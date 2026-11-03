using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Story.Nodes.Audio;
using Inno.VisualNovel.Player.Ports;

namespace Inno.VisualNovel.Player.Audio;

/// <summary>Owns slot replacement, crossfades, category gains and restorable audio policy for one session.</summary>
public sealed class NovelAudioDirector : IDisposable
{
    private sealed record Cue(NovelAudioChannel channel, double volume, bool loop, double fadeIn, double fadeOut, double duration);
    private sealed record Voice(long token, AudioVoiceSnapshot state);
    private readonly Dictionary<string, Cue> m_cues;
    private readonly INovelAudioOutput m_output;
    private readonly HashSet<string> m_characters;
    private Dictionary<string, double> m_characterGains = new(StringComparer.Ordinal);
    private Dictionary<string, double> m_slotMix = new(StringComparer.Ordinal);
    private List<Voice> m_voices = [];
    private readonly double[] m_channels = [1, 1, 1, 1, 1];
    private double m_master = 1;
    private double m_duck = 1;
    private bool m_disposed;
    private bool m_suspended;

    /// <summary>Copies declared cue policy and binds an owner-local audio output.</summary>
    /// <param name="cues">The explicit imported resource library.</param>
    /// <param name="output">The owner-local native adapter or deterministic test output.</param>
    /// <param name="characters">Optional declared character identities permitted in voice snapshots.</param>
    public NovelAudioDirector(
        IEnumerable<AudioCueAsset> cues,
        INovelAudioOutput output,
        IEnumerable<string>? characters = null
    ) {
        ArgumentNullException.ThrowIfNull(output);
        m_output = output;
        m_characters = (characters ?? []).ToHashSet(StringComparer.Ordinal);
        m_cues = cues.ToDictionary(static item => item.resourceId, static item => new Cue(item.channel, item.volume, item.loop, item.fadeIn, item.fadeOut, item.clip?.duration.TotalSeconds ?? 0), StringComparer.Ordinal);
        foreach (var item in m_cues)
            if (string.IsNullOrWhiteSpace(item.Key) || !Enum.IsDefined(item.Value.channel) || !Gain(item.Value.volume) || !Time(item.Value.fadeIn) || !Time(item.Value.fadeOut))
                throw new ArgumentException("Audio cue policy is invalid.", nameof(cues));
    }

    /// <summary>Reports whether any live voice still belongs to a slot.</summary>
    /// <param name="slot">The logical slot.</param>
    /// <returns>True during preparation, playback, pause or fade-out.</returns>
    public bool IsActive(string slot) => m_voices.Any(item => item.state.slot == slot && m_output.IsActive(item.token));
    /// <summary>Suspends this owner's physical voices without changing authored pause state or saved playback policy.</summary>
    /// <param name="suspended">Whether owner time is currently stopped.</param>
    public void SetSuspended(bool suspended)
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
        if (m_suspended == suspended) return;
        foreach (var voice in m_voices) m_output.SetPaused(voice.token, suspended || voice.state.paused);
        m_suspended = suspended;
    }

    /// <summary>Starts a dialogue or history voice, replacing the prior reading voice after successful preparation.</summary>
    /// <param name="cueId">A declared non-looping Voice cue.</param>
    /// <param name="characterId">The optional declared dialogue character, empty for anonymous voices.</param>
    public void PlayVoice(
        string cueId,
        string characterId = ""
    ) {
        ObjectDisposedException.ThrowIf(m_disposed, this);
        if (characterId.Length > 0 && !m_characters.Contains(characterId)) throw new InvalidOperationException("The voiced character is not declared: " + characterId);
        if (!m_cues.TryGetValue(cueId, out Cue? cue) || cue.channel != NovelAudioChannel.Voice || cue.loop)
            throw new InvalidOperationException("Dialogue playback requires a declared non-looping Voice cue: " + cueId);
        if (m_voices.Count(static voice => voice.state.slot != "voice") >= 128) throw new InvalidOperationException("The session audio voice budget of 128 was exceeded.");
        var state = new AudioVoiceSnapshot { slot = "voice", cueId = cueId, characterId = characterId, from = cue.volume, to = cue.volume };
        long token = m_output.Play(cueId, Effective(state), false, 0);
        if (m_suspended) m_output.SetPaused(token, true);
        foreach (Voice old in m_voices.Where(static voice => voice.state.slot == "voice").ToArray()) Remove(old);
        m_voices.Add(new(token, state));
    }

    /// <summary>Stops the reading voice immediately without affecting other audio slots.</summary>
    public void StopVoice()
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
        foreach (Voice voice in m_voices.Where(static voice => voice.state.slot == "voice").ToArray()) Remove(voice);
    }
    /// <summary>Immediately releases a cancelled task's logical slot without starting an authoring fade.</summary>
    /// <param name="slot">The explicitly owned slot identity.</param>
    public void CancelSlot(string slot)
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
        foreach (Voice voice in m_voices.Where(voice => voice.state.slot == slot).ToArray()) Remove(voice);
    }

    /// <summary>Prepares a language-adjusted reading voice snapshot without mutating active voices.</summary>
    /// <param name="snapshot">The captured complete audio state.</param>
    /// <param name="cueId">The current translated line's cue, empty to remove the old-language voice.</param>
    /// <returns>A detached candidate preserving all other slots.</returns>
    public AudioPlaybackSnapshot RetargetReadingVoice(
        AudioPlaybackSnapshot snapshot,
        string cueId
    ) {
        if (!snapshot.voices.Any(static voice => voice.slot == "voice")) return snapshot;
        var voices = snapshot.voices.Where(static voice => voice.slot != "voice").Select(Copy).ToList();
        if (cueId.Length > 0)
        {
            if (!m_cues.TryGetValue(cueId, out Cue? cue) || cue.channel != NovelAudioChannel.Voice || cue.loop)
                throw new InvalidOperationException("Localized voice requires a declared non-looping Voice cue: " + cueId);
            voices.Add(new() { slot = "voice", cueId = cueId, characterId = snapshot.voices.Last(static voice => voice.slot == "voice").characterId, from = cue.volume, to = cue.volume });
        }
        return new() { duckGain = snapshot.duckGain, voices = voices.ToArray() };
    }

    /// <summary>Applies a non-waiting audio request; Wait is observed by the narrative coordinator.</summary>
    /// <param name="request">The detached declarative request.</param>
    public void Apply(AudioNode request)
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
        if (string.IsNullOrWhiteSpace(request.slot) || !Enum.IsDefined(request.operation)) throw new ArgumentException("Audio slot or operation is invalid.", nameof(request));
        Voice[] existing = m_voices.Where(item => item.state.slot == request.slot).ToArray();
        if (request.operation == AudioOperation.Play)
        {
            Cue cue = m_cues[request.cueId];
            if (m_voices.Count >= 128) throw new InvalidOperationException("The session audio voice budget of 128 was exceeded.");
            var state = new AudioVoiceSnapshot { slot = request.slot, cueId = request.cueId, loop = cue.loop, from = cue.fadeIn > 0 ? 0 : cue.volume, to = cue.volume, fadeDuration = cue.fadeIn };
            long token = m_output.Play(request.cueId, Effective(state), cue.loop, 0);
            if (m_suspended) m_output.SetPaused(token, true);
            foreach (Voice item in existing) FadeOut(item);
            m_voices.Add(new(token, state));
        }
        else if (request.operation == AudioOperation.Stop) foreach (Voice item in existing) FadeOut(item);
        else if (request.operation is AudioOperation.Pause or AudioOperation.Resume)
            foreach (Voice item in existing) { m_output.SetPaused(item.token, m_suspended || request.operation == AudioOperation.Pause); item.state.paused = request.operation == AudioOperation.Pause; }
        else if (request.operation == AudioOperation.Wait) throw new ArgumentException("Wait is a narrative boundary, not an immediate audio action.", nameof(request));
    }

    /// <summary>Updates independent master and semantic category gain for this session.</summary>
    /// <param name="master">The zero-to-one master gain.</param>
    /// <param name="channels">Exactly five gains in NovelAudioChannel order.</param>
    public void SetMix(
        double master,
        IReadOnlyList<double> channels
    ) {
        if (!Gain(master) || channels.Count != m_channels.Length || channels.Any(static gain => !Gain(gain))) throw new ArgumentException("Mixer gains must be finite and between zero and one.");
        m_master = master;
        for (int index = 0; index < m_channels.Length; index++) m_channels[index] = channels[index];
        foreach (Voice voice in m_voices) m_output.SetGain(voice.token, Effective(voice.state));
    }
    /// <summary>Applies validated character preferences without changing authored audio state or restoring old settings on rollback.</summary>
    /// <param name="policies">Detached player-owned voice policies.</param>
    public void SetCharacterMix(IEnumerable<Shell.CharacterVoicePreference> policies)
    {
        var validated = new Shell.PlayerPreferences { characterVoices = policies.ToArray() };
        m_characterGains = validated.characterVoices.ToDictionary(static item => item.characterId, static item => item.enabled ? item.volume : 0, StringComparer.Ordinal);
        foreach (Voice voice in m_voices) m_output.SetGain(voice.token, Effective(voice.state));
    }
    /// <summary>Applies owner-supplied timeline multipliers independently of player preferences and cue fades.</summary>
    /// <param name="slots">Finite zero-to-one multipliers keyed by logical audio slot; omitted slots use one.</param>
    public void SetTimelineMix(IReadOnlyDictionary<string, double> slots)
    {
        Dictionary<string, double> candidate = CopyMix(slots);
        if (candidate.Count == m_slotMix.Count && candidate.All(pair => m_slotMix.TryGetValue(pair.Key, out double value) && value == pair.Value)) return;
        m_slotMix = candidate;
        foreach (Voice voice in m_voices) m_output.SetGain(voice.token, Effective(voice.state));
    }

    /// <summary>Advances logical fades and voice positions, releasing natural and faded completions.</summary>
    /// <param name="seconds">A finite nonnegative logical interval.</param>
    public void Tick(double seconds)
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
        if (!Time(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (m_suspended) return;
        bool speaking = m_voices.Any(item => m_cues[item.state.cueId].channel == NovelAudioChannel.Voice && !item.state.paused && m_output.IsActive(item.token));
        double target = speaking ? 0.35 : 1;
        m_duck = target + (m_duck - target) * Math.Exp(-seconds / 0.15);
        foreach (Voice voice in m_voices.ToArray())
        {
            AudioVoiceSnapshot state = voice.state;
            if (!m_output.IsActive(voice.token)) { Remove(voice); continue; }
            if (!state.paused)
            {
                state.position += seconds;
                Cue cue = m_cues[state.cueId];
                if (state.loop && cue.duration > 0) state.position %= cue.duration;
                state.fadeElapsed = Math.Min(state.fadeDuration, state.fadeElapsed + seconds);
            }
            if (state.stopping && state.fadeElapsed >= state.fadeDuration) { Remove(voice); continue; }
            m_output.SetGain(voice.token, Effective(state));
        }
    }

    /// <summary>Captures neutral policy; logical positions may differ from hardware cursors by output latency.</summary>
    /// <returns>Detached voices and fade state without native tokens.</returns>
    public AudioPlaybackSnapshot Capture() => new() { duckGain = m_duck, voices = m_voices.Select(static voice => Copy(voice.state)).ToArray() };

    /// <summary>Prepares muted candidate voices before replacing the current audio output.</summary>
    /// <param name="snapshot">A declaration-matching neutral snapshot.</param>
    public void Restore(AudioPlaybackSnapshot snapshot) => Restore(snapshot, null);

    /// <summary>Prepares voices and an owner-provided timeline mix before replacing either active output state.</summary>
    /// <param name="snapshot">A declaration-matching neutral snapshot.</param>
    /// <param name="timelineMix">Optional candidate timeline multipliers; absent values preserve the current owner's mix.</param>
    public void Restore(
        AudioPlaybackSnapshot snapshot,
        IReadOnlyDictionary<string, double>? timelineMix
    )
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
        Dictionary<string, double> nextMix = CopyMix(timelineMix ?? m_slotMix);
        if (snapshot.voices.Length > 128 || !Gain(snapshot.duckGain)) throw new InvalidOperationException("Audio snapshot exceeds its voice or gain bounds.");
        AudioVoiceSnapshot[] states = snapshot.voices.Select(Copy).ToArray();
        foreach (AudioVoiceSnapshot state in states)
            if (!m_cues.ContainsKey(state.cueId) || state.characterId.Length > 0 && (state.slot != "voice" || !m_characters.Contains(state.characterId))
                || string.IsNullOrWhiteSpace(state.slot) || !Time(state.position) || !Gain(state.from) || !Gain(state.to)
                || !Time(state.fadeDuration) || !Time(state.fadeElapsed) || state.fadeElapsed > state.fadeDuration || state.stopping && state.to != 0)
                throw new InvalidOperationException("An audio snapshot contains invalid resources, gains or time.");
        var candidate = new List<Voice>();
        try
        {
            foreach (AudioVoiceSnapshot state in states)
            {
                long token = m_output.Play(state.cueId, 0, state.loop, state.position);
                candidate.Add(new(token, state));
                if (state.paused || m_suspended) m_output.SetPaused(token, true);
            }
            foreach (Voice voice in candidate) m_output.SetGain(voice.token, Effective(voice.state, snapshot.duckGain, nextMix));
        }
        catch
        {
            foreach (Voice voice in candidate) m_output.Stop(voice.token);
            throw;
        }
        foreach (Voice voice in m_voices) m_output.Stop(voice.token);
        m_voices = candidate; m_duck = snapshot.duckGain; m_slotMix = nextMix;
    }

    /// <summary>Releases every native voice and an explicitly disposable output owned by this session.</summary>
    public void Dispose()
    {
        if (m_disposed) return;
        foreach (Voice voice in m_voices) m_output.Stop(voice.token);
        m_voices.Clear(); m_disposed = true;
        if (m_output is IDisposable lifetime) lifetime.Dispose();
    }

    private void FadeOut(Voice voice)
    {
        double duration = m_cues[voice.state.cueId].fadeOut;
        if (duration == 0) { Remove(voice); return; }
        voice.state.from = CurrentGain(voice.state); voice.state.to = 0;
        voice.state.fadeElapsed = 0; voice.state.fadeDuration = duration; voice.state.stopping = true;
        if (voice.state.paused) { m_output.SetPaused(voice.token, m_suspended); voice.state.paused = false; }
    }
    private void Remove(Voice voice) { m_output.Stop(voice.token); m_voices.Remove(voice); }
    private double Effective(AudioVoiceSnapshot state) => Effective(state, m_duck, m_slotMix);
    private double Effective(
        AudioVoiceSnapshot state,
        double duck,
        IReadOnlyDictionary<string, double> slots
    ) => CurrentGain(state) * m_master * m_channels[(int)m_cues[state.cueId].channel] * (m_cues[state.cueId].channel == NovelAudioChannel.Bgm ? duck : 1)
        * (state.characterId.Length > 0 ? m_characterGains.GetValueOrDefault(state.characterId, 1) : 1) * slots.GetValueOrDefault(state.slot, 1);
    private static Dictionary<string, double> CopyMix(IReadOnlyDictionary<string, double> slots)
    {
        if (slots.Count > 4096 || slots.Any(static pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Key.Length > 256 || !Gain(pair.Value)))
            throw new ArgumentException("Timeline audio mix requires bounded named slots and zero-to-one gains.", nameof(slots));
        return slots.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
    }
    private static double CurrentGain(AudioVoiceSnapshot state) => state.fadeDuration == 0 ? state.to : state.from + (state.to - state.from) * (state.fadeElapsed / state.fadeDuration);
    private static bool Time(double value) => double.IsFinite(value) && value >= 0;
    private static bool Gain(double value) => double.IsFinite(value) && value is >= 0 and <= 1;
    private static AudioVoiceSnapshot Copy(AudioVoiceSnapshot source) => new() { slot = source.slot, cueId = source.cueId, characterId = source.characterId, position = source.position, from = source.from,
        to = source.to, fadeElapsed = source.fadeElapsed, fadeDuration = source.fadeDuration, paused = source.paused, loop = source.loop, stopping = source.stopping };

    /// <summary>Prepares a single non-looping music instance before replacing audio for an isolated gallery playlist.</summary>
    /// <param name="cueId">An available BGM or Ambience cue.</param>
    public void PlayGallery(string cueId)
    {
        if (!m_cues.TryGetValue(cueId, out Cue? cue) || cue.channel is not (NovelAudioChannel.Bgm or NovelAudioChannel.Ambience))
            throw new InvalidOperationException("Music gallery playback requires a declared BGM or Ambience cue.");
        Restore(new() { voices = [new() { slot = "gallery", cueId = cueId, from = cue.volume, to = cue.volume }] });
    }
}

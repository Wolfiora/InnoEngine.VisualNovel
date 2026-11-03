using System;
using InnoEngine.Reflection;
using InnoEngine.Scene;
using InnoEngine.Serialization;
using Inno.VisualNovel.Integration.Services;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Story.Nodes.Audio;
using Inno.VisualNovel.Player.Audio;
using Inno.VisualNovel.Player.Ports;

namespace Inno.VisualNovel.Integration.Components;

/// <summary>Owns scene audio through native object lifetime and the existing VN audio policy; no device handles enter authored or saved state.</summary>
[StableTypeId("b0f32c52-b546-4259-9e9e-d4cd7ebc9c09")]
public sealed class NovelAudioComponent : GameBehavior
{
    private const string C_SLOT = "scene";
    private AudioCueAsset? m_cue;
    private NovelAudioDirector? m_director;
    private double m_gain = 1;
    private double m_playerGain = 1;
    private bool m_paused;

    /// <summary>Gets or sets the imported cue; changing it releases this object's previous playback.</summary>
    [SerializableProperty]
    public AudioCueAsset? cue
    {
        get => m_cue;
        set { if (ReferenceEquals(m_cue, value)) return; Release(); m_cue = value; }
    }
    /// <summary>Gets or sets whether the native runtime enable callback starts an assigned cue. Passive rendering never plays it.</summary>
    [SerializableProperty] public bool playOnEnable { get; set; } = true;
    /// <summary>Gets or sets this object's zero-to-one gain, independently of cue and player gain.</summary>
    [SerializableProperty]
    public double gain
    {
        get => m_gain;
        set { ValidateGain(value); m_gain = value; ApplyMix(); }
    }
    /// <summary>Gets or sets authored pause without replacing the native voice or its position.</summary>
    [SerializableProperty]
    public bool paused
    {
        get => m_paused;
        set
        {
            if (m_paused == value) return;
            m_director?.Apply(new() { slot = C_SLOT, operation = value ? AudioOperation.Pause : AudioOperation.Resume });
            m_paused = value;
        }
    }
    /// <summary>Gets whether this object's slot is preparing, playing, fading or paused.</summary>
    public bool isPlaying => m_director?.IsActive(C_SLOT) ?? false;

    /// <summary>Starts or restarts this cue with its declared fade policy.</summary>
    /// <param name="output">Optional owner-local output transferred to this component on first playback; null selects the public engine Audio adapter. A live component cannot replace its output.</param>
    public void Play(INovelAudioOutput? output = null)
    {
        if (!isActiveAndEnabled) throw new InvalidOperationException("Activate the native audio object before playback.");
        if (m_director is not null)
        {
            if (output is not null) throw new InvalidOperationException("Release existing scene audio before assigning another output.");
            m_director.Apply(new() { slot = C_SLOT, cueId = m_cue!.resourceId, operation = AudioOperation.Play });
            if (paused) m_director.Apply(new() { slot = C_SLOT, operation = AudioOperation.Pause });
            return;
        }
        var candidate = Create(output);
        try
        {
            candidate.Apply(new() { slot = C_SLOT, cueId = m_cue!.resourceId, operation = AudioOperation.Play });
            if (paused) candidate.Apply(new() { slot = C_SLOT, operation = AudioOperation.Pause });
        }
        catch { candidate.Dispose(); throw; }
        m_director = candidate;
    }

    /// <summary>Stops the cue using its declared fade, or immediately releases every owned resource.</summary>
    /// <param name="fade">Whether the component remains alive until the authored fade completes.</param>
    public void Stop(bool fade = true)
    {
        if (!fade) { Release(); return; }
        m_director?.Apply(new() { slot = C_SLOT, operation = AudioOperation.Stop });
    }
    /// <summary>Applies player-owned master/category policy without overwriting authored component gain.</summary>
    /// <param name="value">A zero-to-one multiplier supplied by the owning game's policy.</param>
    public void SetPlayerGain(double value) { ValidateGain(value); m_playerGain = value; ApplyMix(); }
    /// <summary>Advances the existing fade and playback policy during the VN System update.</summary>
    /// <param name="seconds">Finite nonnegative elapsed logical seconds.</param>
    public void Tick(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (!isActiveAndEnabled || m_director is null) return;
        m_director.Tick(seconds);
        if (!m_director.IsActive(C_SLOT)) Release();
    }
    /// <summary>Captures detached cue, fade, pause and playback positions for an explicit scene snapshot owner.</summary>
    /// <returns>Neutral audio state with no voice handles.</returns>
    public AudioPlaybackSnapshot Capture() => m_director?.Capture() ?? new();
    /// <summary>Prepares a candidate snapshot before replacing active playback; failed resource or voice preparation preserves the current sound.</summary>
    /// <param name="snapshot">State captured from this cue declaration.</param>
    /// <param name="output">Optional fresh owner-local output for the candidate, never an output shared with active playback.</param>
    public void Restore(
        AudioPlaybackSnapshot snapshot,
        INovelAudioOutput? output = null
    ) {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!isActiveAndEnabled) throw new InvalidOperationException("Activate the native audio object before restoration.");
        foreach (var voice in snapshot.voices)
            if (voice.slot != C_SLOT) throw new InvalidOperationException("A scene audio snapshot contains another owner's slot.");
        var candidate = Create(output);
        try { candidate.Restore(snapshot); }
        catch { candidate.Dispose(); throw; }
        m_director?.Dispose(); m_director = candidate;
        m_paused = snapshot.voices.Length > 0 && Array.TrueForAll(snapshot.voices, static voice => voice.paused);
    }
    /// <inheritdoc />
    protected override void OnEnable() { if (playOnEnable && cue is not null) Play(); }
    /// <inheritdoc />
    protected override void OnDisable() => Release();
    /// <inheritdoc />
    protected override void OnDestroy() => Release();

    private NovelAudioDirector Create(INovelAudioOutput? output)
    {
        var declaration = cue ?? throw new InvalidOperationException("Assign an audio cue before playback.");
        if (declaration.isMissing || declaration.clip is null || declaration.clip.isMissing)
            throw new InvalidOperationException("The scene audio cue or its imported clip is missing.");
        var actual = output ?? new InnoNovelAudio([declaration]);
        try
        {
            var director = new NovelAudioDirector([declaration], actual);
            director.SetMix(m_gain * m_playerGain, [1, 1, 1, 1, 1]);
            return director;
        }
        catch { if (actual is IDisposable lifetime) lifetime.Dispose(); throw; }
    }
    private void ApplyMix() => m_director?.SetMix(m_gain * m_playerGain, [1, 1, 1, 1, 1]);
    private void Release() { m_director?.Dispose(); m_director = null; }
    private static void ValidateGain(double value)
    { if (!double.IsFinite(value) || value is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(value), "Audio gain must be finite and between zero and one."); }
}

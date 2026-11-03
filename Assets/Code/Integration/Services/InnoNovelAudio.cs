using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InnoEngine.Audio;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Player.Ports;

namespace Inno.VisualNovel.Integration.Services;

/// <summary>Adapts declared cues to public engine voices without changing global mixer state.</summary>
public sealed class InnoNovelAudio : INovelAudioOutput, IDisposable
{
    private readonly Dictionary<string, AudioClipAsset> m_clips;
    private readonly Dictionary<long, AudioVoiceHandle> m_voices = [];
    private readonly Dictionary<AudioClipAsset, Task> m_preloads = [];
    private readonly CancellationTokenSource m_lifetime = new();
    private bool m_disposed;
    private long m_next;
    /// <summary>Copies explicit cue bindings for one session generation.</summary>
    /// <param name="cues">Imported audio roots.</param>
    public InnoNovelAudio(IEnumerable<AudioCueAsset> cues) => m_clips = cues.ToDictionary(static item => item.resourceId,
        static item => item.clip is { isMissing: false } clip ? clip : throw new ArgumentException("An audio cue has no imported clip."), StringComparer.Ordinal);
    /// <inheritdoc />
    public long Play(
        string cueId,
        double gain,
        bool loop,
        double position
    ) {
        ObjectDisposedException.ThrowIf(m_disposed, this);
        AudioClipAsset clip = m_clips[cueId];
        if (!m_preloads.ContainsKey(clip))
            m_preloads.Add(clip, InnoEngine.Audio.Audio.PreloadAsync(clip, AudioClipLoadMode.Stream, m_lifetime.Token).AsTask());
        AudioVoiceHandle voice = InnoEngine.Audio.Audio.Play(clip, new AudioPlayOptions(volume: (float)gain, loop: loop, loadMode: AudioClipLoadMode.Stream));
        try
        {
            if (position > 0 && !InnoEngine.Audio.Audio.Seek(voice, TimeSpan.FromSeconds(loop && clip.duration.TotalSeconds > 0 ? position % clip.duration.TotalSeconds : position)))
                throw new InvalidOperationException("The audio host could not restore a voice position.");
            long token = checked(++m_next); m_voices.Add(token, voice); return token;
        }
        catch { InnoEngine.Audio.Audio.Stop(voice); throw; }
    }
    /// <inheritdoc />
    public void Stop(long token)
    {
        if (m_voices.Remove(token, out AudioVoiceHandle voice)) InnoEngine.Audio.Audio.Stop(voice);
    }
    /// <inheritdoc />
    public void SetGain(
        long token,
        double gain
    ) {
        if (m_voices.TryGetValue(token, out AudioVoiceHandle voice)) InnoEngine.Audio.Audio.SetVoiceParameters(voice, new AudioVoiceParameters((float)gain, 1, 0));
    }
    /// <inheritdoc />
    public void SetPaused(
        long token,
        bool paused
    ) {
        if (!m_voices.TryGetValue(token, out AudioVoiceHandle voice)) return;
        if (paused) InnoEngine.Audio.Audio.Pause(voice); else InnoEngine.Audio.Audio.Resume(voice);
    }
    /// <inheritdoc />
    public bool IsActive(long token) => m_voices.TryGetValue(token, out AudioVoiceHandle voice)
        && InnoEngine.Audio.Audio.TryGetVoiceState(voice, out AudioPlaybackState state) && state is not (AudioPlaybackState.Completed or AudioPlaybackState.Invalid);

    /// <summary>Releases session-owned preload retentions after voices stop and cancels preparations still owned by the native runtime.</summary>
    public void Dispose()
    {
        if (m_disposed) return;
        foreach (long token in m_voices.Keys.ToArray()) Stop(token);
        foreach (var preload in m_preloads)
            if (preload.Value.IsCompletedSuccessfully) InnoEngine.Audio.Audio.ReleasePreload(preload.Key);
        m_disposed = true; m_lifetime.Cancel();
        Task preparations = Task.WhenAll(m_preloads.Values);
        _ = preparations.ContinueWith(task => { _ = task.Exception; m_lifetime.Dispose(); }, CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        m_preloads.Clear();
    }
}

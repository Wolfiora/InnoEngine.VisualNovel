using System;
using System.Linq;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Story.Nodes.Audio;
using Inno.VisualNovel.Player.Audio;
using Inno.VisualNovel.Player.Ports;

namespace Inno.VisualNovel.Authoring.Preview;

/// <summary>Owns explicitly requested audio audition voices without using or changing the running game's mixer.</summary>
/// <param name="output">Creates a separate output owner only when playback is explicitly requested.</param>
public sealed class AudioAudition(Func<AudioCueAsset, INovelAudioOutput> output) : IDisposable
{
    private NovelAudioDirector? m_player;
    private bool m_disposed;
    /// <summary>Gets whether an audition voice remains active.</summary>
    public bool active => m_player?.IsActive("audition") ?? false;
    /// <summary>Gets the currently auditioned clip duration, or zero after stopping.</summary>
    public double duration { get; private set; }
    /// <summary>Gets the logical audition position; it may differ from hardware latency.</summary>
    public double position => m_player?.Capture().voices.FirstOrDefault()?.position ?? 0;
    /// <summary>Gets whether the active audition is paused.</summary>
    public bool paused => m_player?.Capture().voices.FirstOrDefault()?.paused ?? false;
    /// <summary>Starts a detached cue only after an explicit author action, retaining the old audition if preparation fails.</summary>
    /// <param name="cue">The current native or draft cue.</param>
    public void Play(AudioCueAsset cue)
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
        if (cue.clip is not { isMissing: false } clip || clip.duration.TotalSeconds <= 0) throw new InvalidOperationException("Audition requires a valid imported audio clip.");
        var candidate = new NovelAudioDirector([cue], output(cue));
        try { candidate.Apply(new() { operation = AudioOperation.Play, slot = "audition", cueId = cue.resourceId }); }
        catch { candidate.Dispose(); throw; }
        m_player?.Dispose(); m_player = candidate; duration = clip.duration.TotalSeconds;
    }
    /// <summary>Suspends or resumes the audition's own voice.</summary>
    public void TogglePause()
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
        m_player?.Apply(new() { operation = paused ? AudioOperation.Resume : AudioOperation.Pause, slot = "audition" });
    }
    /// <summary>Prepares a clip-relative seek before replacing the currently audible voice.</summary>
    /// <param name="seconds">A finite position between zero and the clip duration.</param>
    public void Seek(double seconds)
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
        if (!double.IsFinite(seconds) || seconds < 0 || seconds > duration || m_player is null) throw new ArgumentOutOfRangeException(nameof(seconds));
        var candidate = m_player.Capture();
        foreach (var voice in candidate.voices) voice.position = Math.Min(seconds, Math.Max(0, duration - .000001));
        m_player.Restore(candidate);
    }
    /// <summary>Advances audition fades and logical time, without starting playback implicitly.</summary>
    /// <param name="seconds">The finite nonnegative owner update interval.</param>
    public void Tick(double seconds)
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        m_player?.Tick(seconds);
    }
    /// <summary>Immediately releases this owner's voices, including looped previews.</summary>
    public void Stop() { m_player?.Dispose(); m_player = null; duration = 0; }
    /// <summary>Stops all preview sound when selection or generation ownership ends.</summary>
    public void Dispose() { if (m_disposed) return; Stop(); m_disposed = true; }
}

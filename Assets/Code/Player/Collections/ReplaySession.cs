using System;
using System.Linq;
using Inno.VisualNovel.Model.Collections;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Narrative.Values;
using Inno.VisualNovel.Player.Audio;
using Inno.VisualNovel.Player.Ports;
using Inno.VisualNovel.Player.Sessions;

namespace Inno.VisualNovel.Player.Collections;

/// <summary>Owns a throwaway replay and suspends its caller's audio without advancing or copying back game state.</summary>
public sealed class ReplaySession : IDisposable
{
    private readonly NovelSession m_caller;
    private readonly AudioPlaybackSnapshot? m_audio;
    private bool m_disposed;
    /// <summary>Prepares an unlocked replay at an explicit authored entry before suspending the caller.</summary>
    /// <param name="project">The same imported content definition as the caller.</param>
    /// <param name="caller">The preserved game session.</param>
    /// <param name="id">An unlocked replay identity.</param>
    /// <param name="audioOutput">A separate output owner, never the caller's adapter.</param>
    public ReplaySession(
        NovelProjectAsset project,
        NovelSession caller,
        string id,
        INovelAudioOutput? audioOutput = null
    ) {
        if (caller.isReplay) throw new InvalidOperationException("Nested replays are unavailable.");
        CollectionEntry entry = caller.collections.Get(id);
        if (!caller.collections.IsUnlocked(id) || entry.kind != CollectionKind.Replay) throw new InvalidOperationException("Replay is locked or unavailable: " + id);
        m_caller = caller; m_audio = caller.audio?.Capture();
        var candidate = new NovelSession(project, audioOutput, replay: true, commands: caller.commands, preview: caller.isPreview, options: caller.ReplayOptions());
        try
        {
            candidate.ApplyPreferences(caller.preferences);
            candidate.SetTheme(caller.themeId);
            candidate.audio?.SetMix(0, candidate.preferences.channelVolumes);
            candidate.RestoreProgress(caller.CaptureProgress());
            candidate.StartAt(entry.chapterId, entry.entryId, entry.initialValues.Select(static initial => new VariableState { id = initial.id, value = initial.value.Copy() }).ToArray());
            if (candidate.story?.status == Narrative.Execution.StoryStatus.Faulted) throw new InvalidOperationException(candidate.story.error);
            candidate.audio?.Tick(0);
            candidate.audio?.SetMix(candidate.preferences.masterVolume, candidate.preferences.channelVolumes);
            caller.audio?.Restore(new());
            session = candidate;
        }
        catch { candidate.Dispose(); throw; }
    }
    /// <summary>Gets the independently owned replay state.</summary>
    public NovelSession session { get; }
    /// <summary>Gets whether the replay has reached its authored end.</summary>
    public bool completed => session.story?.status == Narrative.Execution.StoryStatus.Completed;
    /// <summary>Prepares caller audio before releasing the replay; failure keeps the replay usable for a retry.</summary>
    public void Return()
    {
        if (m_disposed) return;
        if (m_audio is not null) m_caller.audio!.Restore(m_audio);
        session.Dispose(); m_disposed = true;
    }
    /// <summary>Releases throwaway playback without restoring a caller that is itself being destroyed.</summary>
    public void Dispose()
    {
        if (m_disposed) return;
        session.Dispose(); m_disposed = true;
    }
}

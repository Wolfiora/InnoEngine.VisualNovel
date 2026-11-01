namespace Inno.VisualNovel.Model.Performance;

/// <summary>Identifies a deterministic performance lifecycle operation.</summary>
public enum PerformanceOperation
{
    /// <summary>Starts a clip, explicitly replacing previous writers of the same target property.</summary>
    Play,
    /// <summary>Waits until the named non-looping performance completes or is cancelled.</summary>
    Wait,
    /// <summary>Pauses logical playback.</summary>
    Pause,
    /// <summary>Resumes logical playback.</summary>
    Resume,
    /// <summary>Removes the named performance and its property overrides.</summary>
    Cancel,
    /// <summary>Samples a logical position without executing narrative side effects.</summary>
    Seek,
    /// <summary>Waits until the instance first reaches a named clip marker, or is cancelled.</summary>
    WaitMarker
}

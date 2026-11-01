namespace Inno.VisualNovel.Authoring.TimelineEditing;

/// <summary>Identifies a key in one captured native clip without adding persistent editor identities.</summary>
/// <param name="track">The zero-based authored track index.</param>
/// <param name="time">The exact authored key time.</param>
public readonly record struct PerformanceKeyAddress(int track, double time);

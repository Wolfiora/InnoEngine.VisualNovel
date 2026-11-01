using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Stage.Timeline;

namespace Inno.VisualNovel.Authoring.TimelineEditing;

/// <summary>Freezes selected keys and their clip for absolute, nonaccumulating timeline transactions.</summary>
public sealed class PerformanceKeySelection
{
    private readonly PerformanceClipAsset m_source;
    private readonly PerformanceKeyAddress[] m_keys;
    private readonly HashSet<PerformanceKeyAddress> m_members;
    private PerformanceKeySelection(
        PerformanceClipAsset source,
        PerformanceKeyAddress[] keys
    ) {
        m_source = PerformanceClipCopy.Copy(source); m_keys = keys;
        m_members = keys.ToHashSet(); this.keys = Array.AsReadOnly(m_keys);
        minimumOffset = -keys.Min(static key => key.time); maximumOffset = source.duration - keys.Max(static key => key.time);
    }
    /// <summary>Gets the immutable selection in stable track/time order.</summary>
    public IReadOnlyList<PerformanceKeyAddress> keys { get; }
    /// <summary>Gets the earliest legal group displacement, preserving every inter-key interval.</summary>
    public double minimumOffset { get; }
    /// <summary>Gets the latest legal group displacement.</summary>
    public double maximumOffset { get; }
    /// <summary>Validates and copies a nonempty selection from the current document revision.</summary>
    /// <param name="clip">The current detached native clip.</param>
    /// <param name="keys">Exact, unique keys to select.</param>
    /// <returns>A frozen gesture origin; callers must guard the owning document revision before committing.</returns>
    public static PerformanceKeySelection Capture(
        PerformanceClipAsset clip,
        IEnumerable<PerformanceKeyAddress> keys
    ) {
        ArgumentNullException.ThrowIfNull(clip); ArgumentNullException.ThrowIfNull(keys);
        _ = new PerformancePlayer(clip);
        var addresses = keys.ToArray();
        if (addresses.Length == 0 || addresses.Distinct().Count() != addresses.Length || addresses.Any(key => key.track < 0 || key.track >= clip.tracks.Length
            || !double.IsFinite(key.time) || !clip.tracks[key.track].keys.Any(item => item.time == key.time)))
            throw new InvalidOperationException("Select existing unique keys from the current clip revision.");
        return new(clip, addresses.OrderBy(static key => key.track).ThenBy(static key => key.time).ToArray());
    }
    /// <summary>Builds detached moved or copied tracks, rejecting collisions unless replacement was explicitly chosen.</summary>
    /// <param name="offset">An absolute displacement from the captured origin.</param>
    /// <param name="copy">Whether the original keys remain in place.</param>
    /// <param name="replace">Whether to replace unselected keys at destination times.</param>
    /// <returns>Complete validated tracks; failure cannot modify the origin or a document candidate.</returns>
    public PerformanceTrack[] Transform(
        double offset,
        bool copy = false,
        bool replace = false
    ) {
        if (!double.IsFinite(offset) || offset < minimumOffset || offset > maximumOffset)
            throw new InvalidOperationException("The entire selected group must remain inside the clip.");
        var candidate = PerformanceClipCopy.Copy(m_source);
        foreach (var group in m_keys.GroupBy(static key => key.track))
        {
            var original = candidate.tracks[group.Key].keys;
            var moved = original.Where(key => m_members.Contains(new(group.Key, key.time)))
                .Select(key => new PerformanceKeyframe { time = key.time + offset, value = key.value, interpolation = key.interpolation, curve = key.curve.Copy() }).ToArray();
            var retained = original.Where(key => copy || !m_members.Contains(new(group.Key, key.time))).ToArray();
            bool Collides(double time) => moved.Any(key => Near(key.time, time));
            if (retained.Any(key => Collides(key.time)) && !replace)
                throw new InvalidOperationException("Selected keys collide with retained keys; choose another offset or explicitly allow replacement.");
            candidate.tracks[group.Key].keys = retained.Where(key => !Collides(key.time)).Concat(moved).OrderBy(static key => key.time).ToArray();
        }
        _ = new PerformancePlayer(candidate); return candidate.tracks;
    }
    /// <summary>Builds tracks after deleting the selection, requiring explicit permission to remove emptied tracks.</summary>
    /// <param name="removeEmptyTracks">Whether tracks with no remaining keys are also removed.</param>
    /// <returns>Complete independent valid tracks.</returns>
    public PerformanceTrack[] Delete(bool removeEmptyTracks = false)
    {
        var candidate = PerformanceClipCopy.Copy(m_source);
        for (int index = 0; index < candidate.tracks.Length; index++)
        {
            candidate.tracks[index].keys = candidate.tracks[index].keys.Where(key => !m_members.Contains(new(index, key.time))).ToArray();
            if (candidate.tracks[index].keys.Length == 0 && !removeEmptyTracks)
                throw new InvalidOperationException("Deleting the last key requires explicit permission to remove its track.");
        }
        candidate.tracks = candidate.tracks.Where(static track => track.keys.Length > 0).ToArray();
        _ = new PerformancePlayer(candidate); return candidate.tracks;
    }
    private static bool Near(
        double first,
        double second
    ) => Math.Abs(first - second) <= 1e-9 * Math.Max(1, Math.Max(Math.Abs(first), Math.Abs(second)));
}

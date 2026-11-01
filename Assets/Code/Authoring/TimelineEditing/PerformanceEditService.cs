using System;
using System.Linq;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Stage.Timeline;

namespace Inno.VisualNovel.Authoring.TimelineEditing;

/// <summary>Owns unambiguous keyframe and track edits independently of timeline widgets.</summary>
public static class PerformanceEditService
{
    /// <summary>Adds or replaces one stable marker without partially applying invalid clip metadata.</summary>
    /// <param name="clip">The detached native clip candidate.</param>
    /// <param name="marker">The copied synchronization declaration.</param>
    public static void SetMarker(
        PerformanceClipAsset clip,
        PerformanceMarker marker
    ) {
        var candidate = PerformanceClipCopy.Copy(clip);
        candidate.markers = candidate.markers.Where(item => item.id != marker.id).Append(marker.Copy()).OrderBy(static item => item.time).ThenBy(static item => item.id, StringComparer.Ordinal).ToArray();
        _ = new PerformancePlayer(candidate); clip.markers = candidate.markers;
    }
    /// <summary>Removes a selected marker; narrative references remain explicit and diagnose a missing marker when used.</summary>
    /// <param name="clip">The detached clip candidate.</param>
    /// <param name="id">The existing marker identity.</param>
    public static void RemoveMarker(
        PerformanceClipAsset clip,
        string id
    ) {
        if (!clip.markers.Any(marker => marker.id == id)) throw new InvalidOperationException("The selected marker no longer exists.");
        clip.markers = clip.markers.Where(marker => marker.id != id).ToArray();
    }
    /// <summary>Rounds a bounded logical position to an explicit timeline grid.</summary>
    /// <param name="time">The requested finite position.</param>
    /// <param name="step">A finite positive grid interval.</param>
    /// <param name="duration">The clip's positive duration.</param>
    /// <returns>The nearest snapped time, clamped to the clip.</returns>
    public static double Snap(
        double time,
        double step,
        double duration
    ) {
        if (!double.IsFinite(time) || !double.IsFinite(step) || step <= 0 || !double.IsFinite(duration) || duration <= 0)
            throw new ArgumentException("Timeline snapping requires finite time, positive duration and a positive grid.");
        double snapped = Math.Round(Math.Clamp(time, 0, duration) / step, MidpointRounding.AwayFromZero) * step;
        if (!double.IsFinite(snapped)) throw new ArgumentException("The timeline grid is too small for this position.");
        return Math.Clamp(snapped, 0, duration);
    }
    /// <summary>Copies a reviewed time interval into selected tracks with explicit collision replacement.</summary>
    /// <param name="clip">The native transaction candidate.</param>
    /// <param name="tracks">Unique zero-based track indexes.</param>
    /// <param name="start">The inclusive source interval start.</param>
    /// <param name="end">The inclusive source interval end.</param>
    /// <param name="destination">The new interval start.</param>
    /// <param name="replace">Whether the author explicitly permits replacing keys at colliding times.</param>
    /// <returns>The number of copied keys; invalid requests leave the candidate unchanged.</returns>
    public static int CopyRange(
        PerformanceClipAsset clip,
        int[] tracks,
        double start,
        double end,
        double destination,
        bool replace = false
    ) {
        if (tracks.Length == 0 || tracks.Distinct().Count() != tracks.Length || tracks.Any(index => index < 0 || index >= clip.tracks.Length)
            || new[] { start, end, destination }.Any(static value => !double.IsFinite(value)) || start < 0 || end < start || end > clip.duration || destination < 0 || destination + end - start > clip.duration)
            throw new InvalidOperationException("Select valid tracks and source/destination intervals within the clip.");
        var candidate = PerformanceClipCopy.Copy(clip); int count = 0;
        foreach (int index in tracks)
        {
            var original = candidate.tracks[index].keys;
            var copied = original.Where(key => key.time >= start && key.time <= end).Select(key => new PerformanceKeyframe
                { time = destination + key.time - start, value = key.value, interpolation = key.interpolation, curve = key.curve.Copy() }).ToArray();
            bool Collides(double time) => copied.Any(key => Math.Abs(key.time - time) <= 1e-9 * Math.Max(1, Math.Abs(time)));
            if (!replace && original.Any(key => Collides(key.time))) throw new InvalidOperationException("A copied key collides with an existing time; enable explicit replacement or choose another destination.");
            candidate.tracks[index].keys = original.Where(key => !Collides(key.time)).Concat(copied).OrderBy(static key => key.time).ToArray(); count += copied.Length;
        }
        if (count == 0) throw new InvalidOperationException("The selected interval contains no keys.");
        _ = new PerformancePlayer(candidate); clip.tracks = candidate.tracks; return count;
    }
    /// <summary>Adds a scalar property writer with a usable initial keyframe.</summary>
    /// <param name="clip">The candidate clip.</param>
    /// <param name="target">The reusable target slot.</param>
    /// <param name="property">The scalar channel.</param>
    public static void AddTrack(
        PerformanceClipAsset clip,
        string target,
        PerformanceProperty property
    ) {
        if (string.IsNullOrWhiteSpace(target) || !Enum.IsDefined(property) || clip.tracks.Any(item => item.target == target && item.property == property))
            throw new InvalidOperationException("Tracks require a unique target/property pair.");
        clip.tracks = [.. clip.tracks, new() { target = target, property = property, keys = [new() { value = PerformancePropertyRules.Default(property) }] }];
    }
    /// <summary>Inserts or replaces exactly one logical key without changing other keyframe times.</summary>
    /// <param name="clip">The candidate clip.</param>
    /// <param name="trackIndex">The authored track index.</param>
    /// <param name="time">A time within the clip.</param>
    /// <param name="value">The finite scalar value.</param>
    /// <param name="interpolation">Interpolation toward the next key.</param>
    /// <param name="curve">Optional new cubic handles; omitting them preserves an existing key's curve.</param>
    public static void SetKey(
        PerformanceClipAsset clip,
        int trackIndex,
        double time,
        double value,
        PerformanceInterpolation interpolation,
        PerformanceCurve? curve = null
    ) {
        PerformanceTrack track = clip.tracks[trackIndex];
        PerformanceCurve handles = (curve ?? track.keys.SingleOrDefault(item => item.time == time)?.curve ?? new()).Copy(); handles.Validate();
        if (!double.IsFinite(time) || time < 0 || time > clip.duration || !PerformancePropertyRules.IsValid(track.property, value) || !Enum.IsDefined(interpolation))
            throw new ArgumentException("The keyframe is outside the clip or property bounds.");
        track.keys = track.keys.Where(item => item.time != time).Append(new() { time = time, value = value, interpolation = interpolation, curve = handles }).OrderBy(static item => item.time).ToArray();
    }
    /// <summary>Moves one key after checking that its destination is free and within the clip.</summary>
    /// <param name="clip">The candidate clip.</param>
    /// <param name="trackIndex">The authored track index.</param>
    /// <param name="keyIndex">The authored key index.</param>
    /// <param name="time">The new noncolliding logical time.</param>
    public static void MoveKey(
        PerformanceClipAsset clip,
        int trackIndex,
        int keyIndex,
        double time
    ) {
        PerformanceTrack track = clip.tracks[trackIndex];
        if (!double.IsFinite(time) || time < 0 || time > clip.duration || track.keys.Where((_, index) => index != keyIndex).Any(item => item.time == time))
            throw new InvalidOperationException("The keyframe destination is outside the clip or already occupied.");
        track.keys[keyIndex].time = time;
        track.keys = track.keys.OrderBy(static item => item.time).ToArray();
    }
}

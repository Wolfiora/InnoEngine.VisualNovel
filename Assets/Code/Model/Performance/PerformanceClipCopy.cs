using System.Linq;

namespace Inno.VisualNovel.Model.Performance;

/// <summary>Copies reusable performance declarations without retaining source keyframe storage.</summary>
public static class PerformanceClipCopy
{
    /// <summary>Creates a fully independent declaration for session restoration and replay.</summary>
    /// <param name="source">The imported performance.</param>
    /// <returns>A detached performance with copied tracks and keyframes.</returns>
    public static PerformanceClipAsset Copy(PerformanceClipAsset source) => new()
    {
        resourceId = source.resourceId, displayName = source.displayName, duration = source.duration, loop = source.loop, essential = source.essential,
        markers = source.markers.Select(static marker => marker.Copy()).ToArray(),
        tracks = source.tracks.Select(static track => new PerformanceTrack { target = track.target, property = track.property,
            keys = track.keys.Select(static key => new PerformanceKeyframe { time = key.time, value = key.value, interpolation = key.interpolation, curve = key.curve.Copy() }).ToArray() }).ToArray()
    };
}

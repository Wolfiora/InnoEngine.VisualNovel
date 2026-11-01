using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.TimelineEditing;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Stage.Timeline;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class TimelineSelectionContracts
{
    internal static void FrozenGroupAndNativeEvaluation(SerializationRegistry native)
    {
        var clip = Clip();
        var selection = PerformanceKeySelection.Capture(clip, [new(0, 1), new(0, 2), new(1, 1.5)]);
        clip.tracks[0].keys[1].value = 999;
        var moved = Clip(); moved.tracks = selection.Transform(.5);
        Check(moved.tracks[0].keys.Select(static key => key.time).SequenceEqual(new[] { 0d, 1.5, 2.5, 4 })
            && moved.tracks[1].keys.Select(static key => key.time).SequenceEqual(new[] { 0d, 2, 4 }), "Group movement did not preserve inter-key timing and unselected keys.");
        Check(moved.tracks[0].keys[1].value == 10 && moved.tracks[0].keys[2].curve.x1 == .4 && moved.markers.Single().time == 3,
            "A frozen group retained mutable source keys, lost interpolation or moved a synchronization marker.");
        var restored = PerformanceClipCopy.Copy(moved);
        restored.tracks = moved.tracks.Select(track => native.Deserialize<PerformanceTrack>(native.Serialize(track))).ToArray();
        Check(new PerformancePlayer(restored).Evaluate(1.5)[0].value == 10, "The moved native declaration was not evaluated by the production player.");
        moved.tracks[0].keys[2].curve.x1 = 0;
        Check(selection.Transform(.5)[0].keys[2].curve.x1 == .4 && selection.Transform(1)[1].keys[1].time == 2.5,
            "A later absolute drag sample accumulated previous displacement or retained returned track storage.");
        var copied = Clip(); copied.tracks = selection.Transform(.75, copy: true);
        Check(copied.tracks[0].keys.Length == 6 && copied.tracks[1].keys.Length == 4, "A group copy lost its originals or did not copy selected keys.");
    }
    internal static void CollisionsDeletionAndAtomicFailure()
    {
        var clip = Clip(); var selection = PerformanceKeySelection.Capture(clip, [new(0, 1), new(0, 2), new(1, 1.5)]);
        Reject(() => selection.Transform(double.NaN)); Reject(() => selection.Transform(-1.01)); Reject(() => selection.Transform(3.01));
        Reject(() => selection.Transform(1, copy: true)); Reject(() => selection.Transform(2));
        Check(clip.tracks[0].keys.Select(static key => key.time).SequenceEqual(new[] { 0d, 1, 2, 4 }), "A failed edit partially mutated the native candidate.");
        var replaced = selection.Transform(1, copy: true, replace: true);
        Check(replaced[0].keys.Select(static key => key.time).SequenceEqual(new[] { 0d, 1, 2, 3, 4 })
            && replaced[0].keys.Single(key => key.time == 2).value == 10, "Explicit replacement did not replace the retained colliding key deterministically.");
        var removed = selection.Delete(); Check(removed[0].keys.Length == 2 && removed[1].keys.Length == 2, "Deleting a group lost retained keys.");
        var whole = PerformanceKeySelection.Capture(clip, clip.tracks[0].keys.Select(static key => new PerformanceKeyAddress(0, key.time)));
        Reject(() => whole.Delete()); Check(whole.Delete(true).Length == 1, "Empty-track removal did not require and honor explicit permission.");
        Reject(() => PerformanceKeySelection.Capture(clip, [])); Reject(() => PerformanceKeySelection.Capture(clip, [new(0, 9)]));
        Reject(() => PerformanceKeySelection.Capture(clip, [new(0, 1), new(0, 1)]));
    }
    internal static PerformanceClipAsset Clip() => new()
    {
        resourceId = "selected-timeline", duration = 5, markers = [new() { id = "hold", name = "Hold", time = 3 }],
        tracks = [new() { target = "actor", property = PerformanceProperty.X, keys = [new(), new() { time = 1, value = 10 },
            new() { time = 2, value = 20, interpolation = PerformanceInterpolation.CubicBezier, curve = new() { x1 = .4 } }, new() { time = 4, value = 40 }] },
            new() { target = "actor", property = PerformanceProperty.Y, keys = [new(), new() { time = 1.5, value = 15 }, new() { time = 4, value = 40 }] }]
    };
    private static void Reject(Action operation)
    {
        bool rejected = false; try { operation(); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "An invalid group edit was accepted.");
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}

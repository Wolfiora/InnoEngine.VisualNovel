using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.TimelineEditing;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Stage.Timeline;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class CurveContracts
{
    internal static void TimingAndEdits(SerializationRegistry native)
    {
        var curve = new PerformanceCurve();
        Check(PerformanceCurveEvaluator.Evaluate(curve, 0) == 0 && PerformanceCurveEvaluator.Evaluate(curve, 1) == 1
            && PerformanceCurveEvaluator.Evaluate(curve, .5) > .7, "Cubic timing used parameter time instead of solving horizontal progress.");
        var linear = new PerformanceCurve { x1 = 0, y1 = 0, x2 = 1, y2 = 1 };
        double previous = 0;
        for (int step = 0; step <= 100; step++)
        {
            double time = step / 100.0, value = PerformanceCurveEvaluator.Evaluate(curve, time);
            Check(value >= previous && value <= 1 && Math.Abs(PerformanceCurveEvaluator.Evaluate(linear, time) - time) < 1e-10,
                "Normalized cubic timing became nonmonotonic, escaped bounds or changed linear progress."); previous = value;
        }
        var clip = new PerformanceClipAsset { duration = 2, tracks = [new() { target = "actor", property = PerformanceProperty.X,
            keys = [new() { time = 0, value = 0, interpolation = PerformanceInterpolation.CubicBezier, curve = curve }, new() { time = 2, value = 100 }] }] };
        var player = new PerformancePlayer(clip); double middle = player.Evaluate(1).Single().value;
        curve.x1 = 1; curve.y1 = 1; curve.x2 = 1; curve.y2 = 1;
        Check(player.Evaluate(1).Single().value == middle, "A performance retained mutable authoring handles.");
        var key = native.Deserialize<PerformanceKeyframe>(native.Serialize(clip.tracks[0].keys[0]));
        Check(key.curve.x1 == 1 && key.interpolation == PerformanceInterpolation.CubicBezier, "Native keyframe state lost its timing curve.");
        var handles = new PerformanceCurve { x1 = .4, y1 = .2, x2 = .6, y2 = .8 };
        PerformanceEditService.SetKey(clip, 0, 0, 10, PerformanceInterpolation.CubicBezier, handles); handles.x1 = .9;
        PerformanceEditService.MoveKey(clip, 0, 0, .25);
        PerformanceEditService.SetKey(clip, 0, .25, 20, PerformanceInterpolation.CubicBezier);
        Check(clip.tracks[0].keys[0].curve.x1 == .4, "Value edits or key movement reset the existing curve or retained a caller alias.");
        byte[] before = native.Serialize(clip.tracks[0]); bool rejected = false;
        try { PerformanceEditService.SetKey(clip, 0, .5, 10, PerformanceInterpolation.CubicBezier, new() { x1 = double.NaN }); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && native.Serialize(clip.tracks[0]).AsSpan().SequenceEqual(before), "Invalid curve handles partially changed the authored track.");
        clip.tracks[0].keys[0].time = 0;
        foreach (var interpolation in new[] { PerformanceInterpolation.EaseIn, PerformanceInterpolation.EaseOut, PerformanceInterpolation.EaseInOut })
        {
            clip.tracks[0].keys[0].interpolation = interpolation; clip.tracks[0].keys[0].value = 0;
            double sample = new PerformancePlayer(clip).Evaluate(.5).Single().value;
            Check(Math.Abs(sample - (interpolation == PerformanceInterpolation.EaseIn ? 6.25 : interpolation == PerformanceInterpolation.EaseOut ? 43.75 : 12.5)) < 1e-10,
                "Named quadratic easing does not match its declared timing.");
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

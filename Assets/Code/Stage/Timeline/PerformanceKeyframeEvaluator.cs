using System;
using Inno.VisualNovel.Model.Performance;

namespace Inno.VisualNovel.Stage.Timeline;

internal static class PerformanceKeyframeEvaluator
{
    internal static double Sample(
        PerformanceKeyframe[] keys,
        double time
    ) {
        if (time <= keys[0].time) return keys[0].value;
        if (time >= keys[^1].time) return keys[^1].value;
        int right = Array.FindIndex(keys, key => key.time > time);
        PerformanceKeyframe a = keys[right - 1], b = keys[right];
        double amount = Ease(a.interpolation, a.curve, (time - a.time) / (b.time - a.time));
        return a.value * (1 - amount) + b.value * amount;
    }
    internal static double Ease(
        PerformanceInterpolation interpolation,
        PerformanceCurve curve,
        double progress
    ) => interpolation switch
    {
        PerformanceInterpolation.Hold => 0,
        PerformanceInterpolation.Smooth => progress * progress * (3 - 2 * progress),
        PerformanceInterpolation.EaseIn => progress * progress,
        PerformanceInterpolation.EaseOut => 1 - (1 - progress) * (1 - progress),
        PerformanceInterpolation.EaseInOut => progress < .5 ? 2 * progress * progress : 1 - Math.Pow(-2 * progress + 2, 2) / 2,
        PerformanceInterpolation.CubicBezier => PerformanceCurveEvaluator.Evaluate(curve, progress),
        _ => progress
    };
}

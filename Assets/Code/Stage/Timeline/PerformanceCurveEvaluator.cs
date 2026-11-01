using System;
using Inno.VisualNovel.Model.Performance;

namespace Inno.VisualNovel.Stage.Timeline;

/// <summary>Solves normalized cubic timing curves deterministically instead of treating parameter time as horizontal time.</summary>
public static class PerformanceCurveEvaluator
{
    /// <summary>Maps horizontal progress to the corresponding cubic-curve height.</summary>
    /// <param name="curve">Finite normalized handles.</param>
    /// <param name="progress">A normalized horizontal time.</param>
    /// <returns>Normalized eased progress with exact endpoints.</returns>
    public static double Evaluate(
        PerformanceCurve curve,
        double progress
    ) {
        curve.Validate(); if (!double.IsFinite(progress) || progress < 0 || progress > 1) throw new ArgumentOutOfRangeException(nameof(progress));
        if (progress is 0 or 1) return progress;
        double low = 0, high = 1;
        for (int iteration = 0; iteration < 40; iteration++)
        {
            double middle = (low + high) * .5;
            if (Coordinate(middle, curve.x1, curve.x2) < progress) low = middle; else high = middle;
        }
        return Coordinate((low + high) * .5, curve.y1, curve.y2);
    }
    private static double Coordinate(
        double time,
        double a,
        double b
    ) {
        double remaining = 1 - time; return 3 * remaining * remaining * time * a + 3 * remaining * time * time * b + time * time * time;
    }
}

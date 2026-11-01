using System;

namespace Inno.VisualNovel.Model.Performance;

/// <summary>Validates bounded normalized size and color declarations before editing or playback.</summary>
public static class ParticleCurveRules
{
    /// <summary>Rejects unordered times, invalid sizes, ambiguous stops and invalid timing handles.</summary>
    /// <param name="preset">The candidate emitter declaration; empty arrays retain endpoint interpolation.</param>
    public static void Validate(ParticlePreset preset)
    {
        if (preset.sizeKeys.Length > 64 || preset.colorKeys.Length > 64) throw new ArgumentException("A particle lifetime curve supports at most 64 keys per channel.");
        double previous = -1;
        foreach (var key in preset.sizeKeys)
        {
            ValidateKey(key.time, previous, key.interpolation, key.curve);
            if (!double.IsFinite(key.value) || key.value is < 0 or > 1000000) throw new ArgumentException("Particle size keys must stay between zero and one million design pixels.");
            previous = key.time;
        }
        previous = -1;
        foreach (var key in preset.colorKeys) { ValidateKey(key.progress, previous, key.interpolation, key.curve); previous = key.progress; }
    }
    private static void ValidateKey(
        double progress,
        double previous,
        PerformanceInterpolation interpolation,
        PerformanceCurve curve
    ) {
        if (!double.IsFinite(progress) || progress < 0 || progress > 1 || progress <= previous || !Enum.IsDefined(interpolation))
            throw new ArgumentException("Particle curve stops require strictly ordered normalized ages between zero and one.");
        curve.Validate();
    }
}

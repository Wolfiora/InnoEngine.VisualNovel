using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Stage.Timeline;

namespace Inno.VisualNovel.Stage.Effects;

/// <summary>Computes particles directly from birth index, seed and time, independent of frame subdivision.</summary>
public sealed class ParticleSimulator
{
    private readonly ParticlePreset m_preset;
    /// <summary>Validates and freezes a bounded emitter declaration.</summary>
    /// <param name="asset">The explicit native effect source.</param>
    public ParticleSimulator(VisualEffectAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (asset.kind != VisualEffectKind.Particles) throw new ArgumentException("Particle simulator requires a particle preset.", nameof(asset));
        ParticlePreset preset = asset.particles ?? throw new ArgumentException("A visual effect requires its implemented particle definition.", nameof(asset));
        double[] values = [preset.parallax, preset.width, preset.height, preset.rate, preset.duration, preset.minimumLifetime, preset.maximumLifetime,
            preset.minimumVelocityX, preset.maximumVelocityX, preset.minimumVelocityY, preset.maximumVelocityY, preset.accelerationX, preset.accelerationY,
            preset.startSize, preset.endSize, preset.aspect, preset.angularSpeed, preset.minimumRotation, preset.maximumRotation];
        if (string.IsNullOrWhiteSpace(asset.resourceId) || values.Any(static value => !double.IsFinite(value) || Math.Abs(value) > 1000000)
            || !Enum.IsDefined(preset.group) || preset.width < 0 || preset.height < 0 || preset.rate <= 0 || preset.rate > 10000 || preset.duration < 0
            || preset.minimumLifetime < 0.01 || preset.maximumLifetime < preset.minimumLifetime || preset.maximumLifetime > 3600
            || Math.Ceiling(preset.rate * preset.maximumLifetime) + 1 > 4096 || preset.minimumVelocityX > preset.maximumVelocityX
            || preset.minimumVelocityY > preset.maximumVelocityY || preset.startSize < 0 || preset.endSize < 0 || preset.aspect <= 0 || preset.aspect > 100
            || preset.angularSpeed < 0 || preset.minimumRotation > preset.maximumRotation || preset.texture is { } texture && (texture.isMissing || texture.identity.persistentId == Guid.Empty))
            throw new ArgumentException("Particle parameters must be finite, ordered and within the 4096-live-particle budget.", nameof(asset));
        ParticleCurveRules.Validate(preset);
        m_preset = VisualEffectCopy.Copy(asset).particles;
    }

    /// <summary>Samples live particles without consuming random state or changing the emitter.</summary>
    /// <param name="state">A neutral placement, seed and logical time.</param>
    /// <returns>Particles in deterministic birth order.</returns>
    public IReadOnlyList<ParticleSample> Evaluate(ParticleEmitterState state)
    {
        ValidateState(state);
        ParticlePreset p = m_preset;
        double end = state.stopped ? state.emissionEnd : state.elapsed;
        if (p.duration > 0) end = Math.Min(end, p.duration);
        end = Math.Min(end, state.elapsed);
        long first = Math.Max(0, (long)Math.Ceiling((state.elapsed - p.maximumLifetime) * p.rate - 0.5));
        long last = (long)Math.Floor(end * p.rate - 0.5);
        var result = new List<ParticleSample>();
        for (long index = first; index <= last; index++)
        {
            double age = state.elapsed - (index + 0.5) / p.rate;
            double lifetime = Lerp(p.minimumLifetime, p.maximumLifetime, Random(state.seed, index, 0));
            if (age < 0 || age >= lifetime) continue;
            double progress = age / lifetime;
            double x = state.x + p.width * Random(state.seed, index, 1) + age * Lerp(p.minimumVelocityX, p.maximumVelocityX, Random(state.seed, index, 2)) + 0.5 * p.accelerationX * age * age;
            double y = state.y + p.height * Random(state.seed, index, 3) + age * Lerp(p.minimumVelocityY, p.maximumVelocityY, Random(state.seed, index, 4)) + 0.5 * p.accelerationY * age * age;
            double size = p.sizeKeys.Length == 0 ? Lerp(p.startSize, p.endSize, progress) : PerformanceKeyframeEvaluator.Sample(p.sizeKeys, progress);
            double rotation = Lerp(p.minimumRotation, p.maximumRotation, Random(state.seed, index, 5)) + (Random(state.seed, index, 6) * 2 - 1) * p.angularSpeed * age;
            result.Add(new(index, x, y, size * p.aspect, size, rotation, Gradient(p, progress)));
        }
        return result;
    }

    /// <summary>Rejects malformed snapshots before the composition owner commits them.</summary>
    /// <param name="state">The detached emitter candidate.</param>
    public static void ValidateState(ParticleEmitterState state)
    {
        if (string.IsNullOrWhiteSpace(state.id) || string.IsNullOrWhiteSpace(state.presetId) || state.seed == 0
            || new[] { state.elapsed, state.x, state.y, state.emissionEnd }.Any(static value => !double.IsFinite(value) || Math.Abs(value) > 1e12)
            || state.elapsed < 0 || state.emissionEnd < 0 || state.emissionEnd > state.elapsed)
            throw new InvalidOperationException("Particle snapshot has invalid identity, seed, time or placement.");
    }
    private static double Lerp(double a, double b, double amount) => a * (1 - amount) + b * amount;
    private static uint Gradient(
        ParticlePreset preset,
        double progress
    ) {
        var keys = preset.colorKeys;
        if (keys.Length == 0) return Color(preset.startColor, preset.endColor, progress);
        if (progress <= keys[0].progress) return keys[0].color;
        if (progress >= keys[^1].progress) return keys[^1].color;
        int right = Array.FindIndex(keys, key => key.progress > progress);
        var a = keys[right - 1]; var b = keys[right];
        return Color(a.color, b.color, PerformanceKeyframeEvaluator.Ease(a.interpolation, a.curve, (progress - a.progress) / (b.progress - a.progress)));
    }
    private static uint Color(uint a, uint b, double amount)
    {
        uint color = 0;
        for (int shift = 0; shift <= 24; shift += 8) color |= (uint)Math.Round(Lerp((a >> shift) & 255, (b >> shift) & 255, amount)) << shift;
        return color;
    }
    private static double Random(uint seed, long index, uint channel)
    {
        unchecked
        {
            ulong bits = (ulong)index * 0x9E3779B97F4A7C15UL + seed + channel * 0xD1B54A32D192ED03UL;
            bits = (bits ^ (bits >> 30)) * 0xBF58476D1CE4E5B9UL;
            bits = (bits ^ (bits >> 27)) * 0x94D049BB133111EBUL;
            return ((bits ^ (bits >> 31)) >> 11) * (1d / 9007199254740992d);
        }
    }
}

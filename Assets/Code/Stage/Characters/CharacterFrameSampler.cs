using System;
using System.Linq;
using Inno.VisualNovel.Model.Characters;

namespace Inno.VisualNovel.Stage.Characters;

/// <summary>Samples a validated character frame track independently of scene ownership and frame subdivision.</summary>
public static class CharacterFrameSampler
{
    /// <summary>Selects a sprite identity using sequence, blink or explicit speaking timing.</summary>
    /// <param name="part">A validated immutable track declaration.</param>
    /// <param name="elapsed">Nonnegative pose time.</param>
    /// <param name="speechElapsed">Nonnegative time since the current speaking marker.</param>
    /// <param name="speaking">Whether the owner currently has a speaking marker.</param>
    /// <param name="fallback">The authored still image.</param>
    /// <param name="reducedMotion">Whether optional frame motion is suppressed.</param>
    /// <returns>The still image or one of the declared sequence frames.</returns>
    public static Guid Sample(
        CharacterPartAnimationState part,
        double elapsed,
        double speechElapsed,
        bool speaking,
        Guid fallback,
        bool reducedMotion
    ) {
        if (!Enum.IsDefined(part.mode) || !double.IsFinite(elapsed) || elapsed < 0 || !double.IsFinite(speechElapsed) || speechElapsed < 0
            || !double.IsFinite(part.phase) || part.phase < 0 || !double.IsFinite(part.blinkInterval) || part.blinkInterval is < 0 or > 3600
            || part.frames.Length > 256 || part.frames.Any(static frame => !double.IsFinite(frame.seconds) || frame.seconds is < .001 or > 3600))
            throw new InvalidOperationException("Character frame timing is invalid.");
        if (part.mode == CharacterAnimationMode.None || reducedMotion && !part.essential) return fallback;
        if (part.mode == CharacterAnimationMode.Talking && !speaking) return fallback;
        double duration = part.frames.Sum(static frame => frame.seconds);
        if (part.frames.Length == 0 || !double.IsFinite(duration) || duration <= 0)
            throw new InvalidOperationException("A character animation requires positive frame durations.");
        double clock = part.mode == CharacterAnimationMode.Talking ? speechElapsed : elapsed + part.phase;
        bool loop = part.mode != CharacterAnimationMode.Sequence || part.loop;
        double cycle = duration + (part.mode == CharacterAnimationMode.Blink ? part.blinkInterval : 0);
        if (!loop && clock >= cycle) return part.frames[^1].texture;
        double position = loop ? clock % cycle : clock;
        if (cycle - position <= 1e-9) position = 0;
        if (part.mode == CharacterAnimationMode.Blink)
        {
            if (position + 1e-9 < part.blinkInterval) return fallback;
            position -= part.blinkInterval;
        }
        double end = 0;
        foreach (var frame in part.frames) { end += frame.seconds; if (position + 1e-9 < end) return frame.texture; }
        return part.frames[^1].texture;
    }
    /// <summary>Computes a stable normalized phase without consuming narrative random state.</summary>
    /// <param name="identity">An explicit stable owner identity.</param>
    /// <returns>A deterministic value in the half-open zero-to-one interval.</returns>
    public static double Phase(string identity)
    {
        uint hash = 2166136261;
        foreach (char value in identity) { hash = unchecked((hash ^ (byte)value) * 16777619); hash = unchecked((hash ^ (byte)(value >> 8)) * 16777619); }
        return hash / ((double)uint.MaxValue + 1);
    }
}

using System;
using InnoEngine.Mathematics;
using Inno.VisualNovel.Model.Interface.Animation;

namespace Inno.VisualNovel.UI.Animation;

/// <summary>Samples pure UI presentation using engine mathematics, without layout mutation or actions.</summary>
public static class UiMotionSampling
{
    /// <summary>Samples one authored event at an absolute event-local time.</summary>
    /// <param name="motion">The validated timing declaration.</param>
    /// <param name="seconds">Nonnegative logical event time.</param>
    /// <param name="entering">Whether to travel from the hidden pose to the ordinary pose.</param>
    /// <returns>The detached relative pose; disabled motion uses the ordinary pose.</returns>
    public static UiMotionPose Sample(
        UiMotionDefinition motion,
        double seconds,
        bool entering = false
    ) {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (!motion.enabled) return new();
        float amount = Progress(motion, seconds);
        return entering ? Blend(motion.pose, new(), amount) : Blend(new(), motion.pose, amount);
    }
    internal static float Progress(
        UiMotionDefinition motion,
        double seconds
    ) {
        float t = seconds < motion.delay ? 0 : motion.duration == 0 ? 1 : (float)Math.Clamp((seconds - motion.delay) / motion.duration, 0, 1);
        return motion.easing switch { UiMotionEasing.Smooth => MathHelper.SmoothStep(0, 1, t), UiMotionEasing.EaseIn => t * t, UiMotionEasing.EaseOut => 1 - (1 - t) * (1 - t), _ => t };
    }
    internal static UiMotionPose Blend(
        UiMotionPose from,
        UiMotionPose to,
        float amount
    ) => new() { opacity = MathHelper.Lerp(from.opacity, to.opacity, amount), x = MathHelper.Lerp(from.x, to.x, amount), y = MathHelper.Lerp(from.y, to.y, amount), scale = MathHelper.Lerp(from.scale, to.scale, amount) };
    internal static UiMotionPose Combine(
        UiMotionPose first,
        UiMotionPose second
    ) => new() { opacity = first.opacity * second.opacity, x = first.x + second.x, y = first.y + second.y, scale = first.scale * second.scale };

    /// <summary>Builds a relative transform around an element's resolved layout pivot.</summary>
    /// <param name="bounds">The original layout rectangle.</param>
    /// <param name="pivot">The normalized authored pivot.</param>
    /// <param name="pose">The sampled relative channels.</param>
    /// <returns>An engine matrix used by painting and hit testing.</returns>
    public static Matrix Transform(
        Rect bounds,
        Vector2 pivot,
        UiMotionPose pose
    ) {
        float x = bounds.x + bounds.width * pivot.x, y = bounds.y + bounds.height * pivot.y;
        return Matrix.CreateTranslation(x + pose.x, y + pose.y, 0) * Matrix.CreateScale(pose.scale, pose.scale, 1) * Matrix.CreateTranslation(-x, -y, 0);
    }
    /// <summary>Projects an axis-aligned rectangle through the supported positive uniform scale and translation.</summary>
    /// <param name="bounds">The original rectangle.</param>
    /// <param name="transform">The composed engine presentation matrix.</param>
    /// <returns>A finite projected rectangle.</returns>
    public static Rect Bounds(
        Rect bounds,
        Matrix transform
    ) {
        Vector2 first = Vector2.Transform(new(bounds.x, bounds.y), transform), last = Vector2.Transform(new(bounds.x + bounds.width, bounds.y + bounds.height), transform);
        if (!float.IsFinite(first.x) || !float.IsFinite(first.y) || !float.IsFinite(last.x) || !float.IsFinite(last.y) || Math.Abs(last.x - first.x) > 1e8 || Math.Abs(last.y - first.y) > 1e8)
            throw new InvalidOperationException("Nested UI presentation exceeded the finite geometry budget.");
        return new(first.x, first.y, last.x - first.x, last.y - first.y);
    }
}

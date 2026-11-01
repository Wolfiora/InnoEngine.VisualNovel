using System.Linq;

namespace Inno.VisualNovel.Model.Performance;

/// <summary>Freezes particle declarations while borrowing only canonical engine texture references.</summary>
public static class VisualEffectCopy
{
    /// <summary>Copies all emitter parameters independently of later authoring edits.</summary>
    /// <param name="source">The imported or drafted preset.</param>
    /// <returns>An independently owned declaration.</returns>
    public static VisualEffectAsset Copy(VisualEffectAsset source) => new()
    {
        resourceId = source.resourceId, displayName = source.displayName, description = source.description, kind = source.kind,
        screen = source.screen.Copy(), transition = source.transition.Copy(), particles = new()
        {
            texture = source.particles.texture, group = source.particles.group, order = source.particles.order, parallax = source.particles.parallax,
            width = source.particles.width, height = source.particles.height, rate = source.particles.rate, duration = source.particles.duration,
            minimumLifetime = source.particles.minimumLifetime, maximumLifetime = source.particles.maximumLifetime,
            minimumVelocityX = source.particles.minimumVelocityX, maximumVelocityX = source.particles.maximumVelocityX, minimumVelocityY = source.particles.minimumVelocityY, maximumVelocityY = source.particles.maximumVelocityY,
            accelerationX = source.particles.accelerationX, accelerationY = source.particles.accelerationY, startSize = source.particles.startSize, endSize = source.particles.endSize, aspect = source.particles.aspect,
            minimumRotation = source.particles.minimumRotation, maximumRotation = source.particles.maximumRotation,
            angularSpeed = source.particles.angularSpeed, startColor = source.particles.startColor, endColor = source.particles.endColor, essential = source.particles.essential,
            sizeKeys = source.particles.sizeKeys.Select(static key => new PerformanceKeyframe { time = key.time, value = key.value, interpolation = key.interpolation, curve = key.curve.Copy() }).ToArray(),
            colorKeys = source.particles.colorKeys.Select(static key => key.Copy()).ToArray()
        }
    };
}

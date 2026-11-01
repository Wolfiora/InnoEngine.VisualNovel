using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Inno.VisualNovel.Stage.Composition;

namespace Inno.VisualNovel.Stage.Effects;

/// <summary>Converts deterministic particles into the same transformed quads used by ordinary stage layers.</summary>
public static class ParticleFrameBuilder
{
    /// <summary>Builds one emitter's sprites using the stage camera's current affine transform.</summary>
    /// <param name="batch">The detached particle batch.</param>
    /// <param name="camera">The camera matrix for this emitter's parallax.</param>
    /// <returns>Sprites in birth order.</returns>
    public static IReadOnlyList<StageDrawItem> Build(
        ParticleDrawBatch batch,
        Matrix3x2 camera
    ) {
        var result = new List<StageDrawItem>(batch.samples.Count);
        foreach (ParticleSample particle in batch.samples)
        {
            Matrix3x2 transform = Matrix3x2.CreateTranslation((float)(-particle.width / 2), (float)(-particle.height / 2))
                * Matrix3x2.CreateRotation((float)(particle.rotation * Math.PI / 180)) * Matrix3x2.CreateTranslation((float)particle.x, (float)particle.y) * camera;
            result.Add(new("particles/" + batch.id + "/" + particle.index.ToString(CultureInfo.InvariantCulture),
                Vector2.Transform(Vector2.Zero, transform), Vector2.Transform(new((float)particle.width, 0), transform),
                Vector2.Transform(new((float)particle.width, (float)particle.height), transform), Vector2.Transform(new(0, (float)particle.height), transform), batch.texture, particle.color));
        }
        return result;
    }
}

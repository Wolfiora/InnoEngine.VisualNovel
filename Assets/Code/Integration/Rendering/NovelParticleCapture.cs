using System.Collections.Generic;
using InnoEngine.Mathematics;
using Inno.VisualNovel.Integration.Components;
using Inno.VisualNovel.Stage.Composition;
using Inno.VisualNovel.Stage.Effects;
using NumericsVector2 = System.Numerics.Vector2;

namespace Inno.VisualNovel.Integration.Rendering;

internal static class NovelParticleCapture
{
    internal static IEnumerable<StageDrawItem> Capture(
        NovelParticleComponent emitter,
        NovelSceneProjection view
    ) {
        if (!emitter.visible || emitter.preset is null) yield break;
        var preset = emitter.preset.particles;
        var samples = new ParticleSimulator(emitter.preset).Evaluate(emitter.Capture());
        var batch = new ParticleDrawBatch(emitter.identity.persistentId.ToString(), preset.group, preset.order, preset.parallax, preset.texture, samples);
        Matrix matrix = emitter.transform.localToWorldMatrix;
        NumericsVector2 Project(NumericsVector2 local) => view.Project(Vector3.Transform(new(local.X, -local.Y, 0), matrix), preset.parallax);
        foreach (var item in ParticleFrameBuilder.Build(batch, System.Numerics.Matrix3x2.Identity))
            yield return item with
            {
                topLeft = Project(item.topLeft), topRight = Project(item.topRight),
                bottomRight = Project(item.bottomRight), bottomLeft = Project(item.bottomLeft),
                owner = emitter.gameObject.identity.persistentId.ToString()
            };
    }
}

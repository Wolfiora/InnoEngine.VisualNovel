using System.Collections.Generic;
using InnoEngine.Rendering;
using Inno.VisualNovel.Model.Stages;

namespace Inno.VisualNovel.Stage.Effects;

/// <summary>Groups immutable particle samples with borrowed composition resources.</summary>
/// <param name="id">The emitter identity.</param>
/// <param name="group">The stage layer band.</param>
/// <param name="order">Composition order within the band.</param>
/// <param name="parallax">Camera translation response.</param>
/// <param name="texture">The optional canonical sprite.</param>
/// <param name="samples">Particles in stable birth order.</param>
public sealed record ParticleDrawBatch(string id, StageLayerGroup group, int order, double parallax, TextureAsset? texture, IReadOnlyList<ParticleSample> samples);

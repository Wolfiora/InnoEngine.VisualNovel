using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Inno.VisualNovel.Rendering.Frames;

/// <summary>Evaluates one declared two-image shader operation.</summary>
/// <param name="source">The current premultiplied image.</param>
/// <param name="previous">The previous image, or the source for single-image operations.</param>
/// <param name="config">Operation kind, strength, pattern and reverse policy.</param>
/// <param name="parameters">Operation-specific scalar parameters.</param>
/// <param name="color">Packed RRGGBBAA overlay color.</param>
/// <param name="seed">Deterministic procedural pattern seed.</param>
public sealed record NovelProcessImage(NovelImagePlan source, NovelImagePlan previous, Vector4 config, Vector4 parameters,
    uint color = 0xFFFFFFFF, uint seed = 0) : NovelImagePlan
{
    /// <inheritdoc />
    public override IEnumerable<NovelDrawQuad> Paints() => source.Paints().Concat(ReferenceEquals(source, previous) ? [] : previous.Paints());
    /// <inheritdoc />
    public override IEnumerable<NovelDrawMesh> Meshes() => source.Meshes().Concat(ReferenceEquals(source, previous) ? [] : previous.Meshes());
}

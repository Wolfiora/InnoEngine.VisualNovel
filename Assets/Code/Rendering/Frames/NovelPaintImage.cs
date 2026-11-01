using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Inno.VisualNovel.Rendering.Frames;

/// <summary>Paints original straight-alpha sprites and glyphs into a transparent image.</summary>
/// <param name="quads">Ordered source geometry.</param>
public sealed record NovelPaintImage(IReadOnlyList<NovelDrawQuad> quads) : NovelImagePlan
{
    /// <inheritdoc />
    public override IEnumerable<NovelDrawQuad> Paints() => quads;
}


using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Inno.VisualNovel.Rendering.Frames;

/// <summary>Composites premultiplied images in back-to-front order.</summary>
/// <param name="layers">Ordered independent images.</param>
public sealed record NovelStackImage(IReadOnlyList<NovelImagePlan> layers) : NovelImagePlan
{
    /// <inheritdoc />
    public override IEnumerable<NovelDrawQuad> Paints() => layers.SelectMany(static item => item.Paints());
}


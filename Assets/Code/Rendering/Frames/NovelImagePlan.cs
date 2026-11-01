using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Inno.VisualNovel.Rendering.Frames;

/// <summary>Describes a pure premultiplied image computation without GPU ownership.</summary>
public abstract record NovelImagePlan
{
    /// <summary>Enumerates original paint commands needed by every branch of this image.</summary>
    /// <returns>Commands including frozen previous compositions.</returns>
    public abstract IEnumerable<NovelDrawQuad> Paints();
}


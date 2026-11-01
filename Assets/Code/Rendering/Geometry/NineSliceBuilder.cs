using InnoEngine.Mathematics;
using System;
using System.Collections.Generic;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Rendering.Frames;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Rendering.Geometry;

/// <summary>Preserves source border pixels while stretching only the interior strips of an image.</summary>
public static class NineSliceBuilder
{
    /// <summary>Produces nonoverlapping geometry and exact normalized source rectangles, including compressed small destinations.</summary>
    /// <param name="texture">The immutable source image description.</param>
    /// <param name="sourceWidth">Positive source pixel width.</param>
    /// <param name="sourceHeight">Positive source pixel height.</param>
    /// <param name="bounds">The destination design rectangle.</param>
    /// <param name="clip">The effective UI clip.</param>
    /// <param name="color">Image tint including complete element opacity.</param>
    /// <param name="slice">The declared source borders.</param>
    /// <returns>At most nine original-alpha paint commands in row order.</returns>
    public static IReadOnlyList<NovelDrawQuad> Build(
        NovelTextureSource texture,
        int sourceWidth,
        int sourceHeight,
        Rect bounds,
        Rect clip,
        uint color,
        UiImageSlice slice
    ) {
        if (sourceWidth <= 0 || sourceHeight <= 0 || !float.IsFinite(bounds.x) || !float.IsFinite(bounds.y) || !float.IsFinite(bounds.width) || !float.IsFinite(bounds.height) || bounds.width < 0 || bounds.height < 0)
            throw new ArgumentException("Nine-slice images require a finite nonnegative destination and positive source dimensions.");
        slice.Validate(sourceWidth, sourceHeight);
        float horizontal = Math.Min(1, bounds.width / Math.Max(.00001f, (slice.left + slice.right) * slice.scale));
        float vertical = Math.Min(1, bounds.height / Math.Max(.00001f, (slice.top + slice.bottom) * slice.scale));
        float[] x = [bounds.x, bounds.x + slice.left * slice.scale * horizontal, bounds.x + bounds.width - slice.right * slice.scale * horizontal, bounds.x + bounds.width];
        float[] y = [bounds.y, bounds.y + slice.top * slice.scale * vertical, bounds.y + bounds.height - slice.bottom * slice.scale * vertical, bounds.y + bounds.height];
        float[] u = [0, slice.left / sourceWidth, 1 - slice.right / sourceWidth, 1];
        float[] v = [0, slice.top / sourceHeight, 1 - slice.bottom / sourceHeight, 1];
        var result = new List<NovelDrawQuad>();
        for (int row = 0; row < 3; row++)
            for (int column = 0; column < 3; column++)
            {
                if (!slice.drawCenter && row == 1 && column == 1 || x[column + 1] <= x[column] || y[row + 1] <= y[row] || u[column + 1] <= u[column] || v[row + 1] <= v[row]) continue;
                result.Add(new(new(x[column], y[row], x[column + 1] - x[column], y[row + 1] - y[row]), clip, color, texture,
                    uv: new(u[column], v[row], u[column + 1] - u[column], v[row + 1] - v[row])));
            }
        return result.AsReadOnly();
    }
}

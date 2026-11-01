using InnoEngine.Mathematics;
using System;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.UI.Layout;

/// <summary>Evaluates centered image placement independently of GPU geometry and input bounds.</summary>
public static class UiImageSizing
{
    /// <summary>Fits positive source dimensions into an authored rectangle.</summary>
    /// <param name="bounds">The destination and clipping bounds.</param>
    /// <param name="sourceWidth">The positive imported pixel width.</param>
    /// <param name="sourceHeight">The positive imported pixel height.</param>
    /// <param name="fit">The declared sizing rule.</param>
    /// <returns>The complete image rectangle; Cover requires clipping to the authored bounds.</returns>
    public static Rect Fit(
        Rect bounds,
        int sourceWidth,
        int sourceHeight,
        UiImageFit fit
    ) {
        if (sourceWidth <= 0 || sourceHeight <= 0 || !Enum.IsDefined(fit)) throw new ArgumentException("Image sizing requires positive source dimensions and a known fit rule.");
        if (fit is UiImageFit.Stretch or UiImageFit.NineSlice) return bounds;
        float scale = fit == UiImageFit.Contain ? Math.Min(bounds.width / sourceWidth, bounds.height / sourceHeight) : Math.Max(bounds.width / sourceWidth, bounds.height / sourceHeight);
        float width = sourceWidth * scale, height = sourceHeight * scale;
        return new(bounds.x + (bounds.width - width) / 2, bounds.y + (bounds.height - height) / 2, width, height);
    }
}

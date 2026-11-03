using System;
using InnoEngine.Rendering;
using InnoEngine.UI;
using Inno.VisualNovel.UI.Layout;

namespace Inno.VisualNovel.Integration.Services;

/// <summary>Maps a host viewport into the same logical output used by native canvas rendering.</summary>
public static class InnoNovelSceneInput
{
    /// <summary>Preserves physical engine input while excluding letterbox regions; suspension requires input release.</summary>
    /// <param name="input">One host-owned viewport sample.</param>
    /// <param name="viewport">The viewport's physical extent.</param>
    /// <param name="width">Logical output width.</param>
    /// <param name="height">Logical output height.</param>
    /// <returns>Detached engine UI input, or null when the host suspends interaction.</returns>
    public static UiInputSnapshot? Map(
        RenderOutputInput input,
        RenderViewport viewport,
        float width,
        float height
    ) {
        if (!input.interactionEnabled) return null;
        var fit = UiCanvasFit.Create(width, height, new(0, 0, viewport.width, viewport.height), true);
        var point = fit.Unproject(new(input.pointerPosition.x, input.pointerPosition.y));
        bool inside = input.pointerInside && point.X >= 0 && point.Y >= 0 && point.X < width && point.Y < height;
        return new(inside ? new(point.X, point.Y) : new(-100000, -100000), inside ? input.scrollDelta : default,
            input.modifiers, input.keysPressed, input.keysReleased, inside ? input.buttonsPressed : [], input.buttonsReleased, input.textInput);
    }
}

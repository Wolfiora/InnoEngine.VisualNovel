using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.UI.Theming;

/// <summary>Builds accessible paint variants without changing authored layout, input, images or story state.</summary>
public static class UiHighContrastPalette
{
    /// <summary>Recolors one completed paint item, including rich-text overrides and transient dropdown/help entries.</summary>
    /// <param name="item">The immutable ordinary paint command.</param>
    /// <returns>A detached high-contrast variant with identical geometry, resources and reveal positions.</returns>
    public static UiDrawItem Apply(UiDrawItem item)
    {
        bool hasText = item.text.Length > 0 || item.richText?.document.text.Length > 0;
        bool control = item.kind is UiElementKind.Button or UiElementKind.Toggle or UiElementKind.Slider or UiElementKind.Selector or UiElementKind.Input or UiElementKind.Progress;
        uint background = item.background;
        if (hasText || control && (background & 255) > 0 || item.kind == UiElementKind.Panel && (background & 255) > 0)
            background = control && (background & 0xFFFFFF00) != 0x00000000 ? 0x102A43FFu : 0x000000FFu;
        return item with
        {
            background = background, foreground = 0xFFFFFFFF, focusColor = 0xFFEB3BFF,
            richText = item.richText is { } rich ? rich with { document = rich.document.WithForeground(0xFFFFFFFF) } : null
        };
    }
}

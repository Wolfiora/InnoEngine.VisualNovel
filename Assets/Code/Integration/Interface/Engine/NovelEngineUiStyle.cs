using System;
using System.Globalization;
using System.Text;
using InnoEngine.Scene;
using Inno.VisualNovel.Integration.Components.Interface;
using Inno.VisualNovel.Model.Interface;

namespace Inno.VisualNovel.Integration.Interface.Engine;

/// <summary>Encodes layout declarations for the bundled RML document language, without measuring controls.</summary>
internal static class NovelEngineUiStyle
{
    internal static string Build(
        GameObject owner,
        UiContainerLayout parentLayout,
        bool enabled
    ) {
        owner.TryGetComponent<NovelUiLayoutComponent>(out var component);
        var layout = component?.Capture() ?? new UiLayoutDefinition { anchorX = .5f, anchorY = .5f, pivotX = .5f, pivotY = .5f };
        var transform = owner.transform;
        bool automatic = parentLayout != UiContainerLayout.Absolute;
        var position = transform.localPosition;
        var scale = transform.localScale;
        var rotation = transform.localRotation.ToEulerAnglesXYZDegrees();
        if (Math.Abs(rotation.x) > .001f || Math.Abs(rotation.y) > .001f || Math.Abs(position.z) > .001f)
            throw new NotSupportedException("Engine UI scene controls currently require planar native transforms.");
        var style = new StringBuilder("box-sizing:border-box;");
        style.Append(automatic ? "position:relative;" : "position:absolute;");
        if (automatic) style.Append("left:").Append(N(position.x)).Append("px;top:").Append(N(-position.y)).Append("px;");
        else style.Append("left:").Append(N(layout.anchorX * 100)).Append("%;top:").Append(N(layout.anchorY * 100)).Append("%;");
        style.Append("width:").Append(Size(layout.widthMode, layout.width)).Append(";height:").Append(Size(layout.heightMode, layout.height)).Append(';');
        if (parentLayout == UiContainerLayout.Horizontal && layout.widthMode == UiSizeMode.Fill
            || parentLayout == UiContainerLayout.Vertical && layout.heightMode == UiSizeMode.Fill) style.Append("flex:1 1 0px;");
        else if (automatic) style.Append("flex-shrink:0;");
        if (automatic) style.Append("align-self:").Append((parentLayout == UiContainerLayout.Horizontal ? layout.verticalAlignment : layout.horizontalAlignment) switch
        {
            UiAlignment.Start => "flex-start", UiAlignment.Center => "center", UiAlignment.End => "flex-end", UiAlignment.Stretch => "stretch",
            _ => throw new InvalidOperationException("Unknown layout alignment.")
        }).Append(';');
        style.Append("min-width:").Append(N(layout.minWidth)).Append("px;min-height:").Append(N(layout.minHeight)).Append("px;");
        style.Append("max-width:").Append(N(layout.maxWidth)).Append("px;max-height:").Append(N(layout.maxHeight)).Append("px;");
        style.Append("margin:").Append(N(layout.marginTop)).Append("px ").Append(N(layout.marginRight)).Append("px ")
            .Append(N(layout.marginBottom)).Append("px ").Append(N(layout.marginLeft)).Append("px;");
        style.Append("transform-origin:").Append(N(layout.pivotX * 100)).Append("% ").Append(N(layout.pivotY * 100)).Append("%;transform:");
        if (!automatic) style.Append("translate(").Append(N(position.x)).Append("px,").Append(N(-position.y)).Append("px) translate(")
            .Append(N(-layout.pivotX * 100)).Append("%,").Append(N(-layout.pivotY * 100)).Append("%) ");
        style.Append("rotate(").Append(N(-rotation.z)).Append("deg) scale(").Append(N(scale.x)).Append(',').Append(N(scale.y)).Append(");");
        style.Append("opacity:").Append(N(component?.opacity ?? 1)).Append(';');
        if (layout.clip) style.Append("overflow:hidden;");
        if (owner.TryGetComponent<NovelUiScrollComponent>(out var scroll) && scroll is not null)
            style.Append("overflow-x:").Append(scroll.horizontal ? "auto" : "hidden").Append(";overflow-y:").Append(scroll.vertical ? "auto" : "hidden").Append(';');
        owner.TryGetComponent<NovelUiPanelComponent>(out var panel);
        bool control = owner.TryGetComponent<NovelUiButtonComponent>(out _) || owner.TryGetComponent<NovelUiToggleComponent>(out _);
        style.Append(enabled && (control || panel?.blocksPointer != false) ? "pointer-events:auto;" : "pointer-events:none;");
        if (!enabled) style.Append("tab-index:none;");
        if (enabled && (owner.TryGetComponent<NovelUiButtonComponent>(out _) || owner.TryGetComponent<NovelUiToggleComponent>(out _))) style.Append("tab-index:auto;");
        if (panel is not null)
            style.Append("background-color:").Append(Color(panel.color)).Append(';');
        if (owner.TryGetComponent<NovelUiContainerComponent>(out var container) && container is not null && container.layout != UiContainerLayout.Absolute)
        {
            if (!float.IsFinite(container.padding) || container.padding < 0 || !float.IsFinite(container.gap) || container.gap < 0)
                throw new InvalidOperationException("UI container spacing must be finite and nonnegative.");
            style.Append("display:flex;flex-direction:").Append(container.layout == UiContainerLayout.Horizontal ? "row" : "column")
                .Append(";padding:").Append(N(container.padding)).Append("px;gap:").Append(N(container.gap)).Append("px;justify-content:")
                .Append(container.alignment switch { UiFlowAlignment.Start => "flex-start", UiFlowAlignment.Center => "center", UiFlowAlignment.End => "flex-end",
                    UiFlowAlignment.SpaceBetween => "space-between", UiFlowAlignment.SpaceAround => "space-around", _ => throw new InvalidOperationException("Unknown flow alignment.") }).Append(';');
        }
        if (owner.TryGetComponent<NovelUiTextComponent>(out var text) && text is not null)
        {
            if (text.fontSize <= 0 || text.lineSpacing <= 0 || text.padding < 0) throw new InvalidOperationException("Invalid UI typography.");
            style.Append("color:").Append(Color(text.color)).Append(";font-size:").Append(N(text.fontSize)).Append("px;line-height:")
                .Append(N(text.lineSpacing)).Append(";padding:").Append(N(text.padding)).Append("px;text-align:")
                .Append(text.alignment switch { UiTextAlignment.Left => "left", UiTextAlignment.Center => "center", UiTextAlignment.Right => "right",
                    _ => throw new InvalidOperationException("Unknown text alignment.") }).Append(';');
        }
        return style.ToString();
    }

    private static string Size(
        UiSizeMode mode,
        float value
    ) => mode switch
    {
        UiSizeMode.Fixed => N(value) + "px", UiSizeMode.Fill => "100%", UiSizeMode.Content => "auto",
        _ => throw new InvalidOperationException("Unknown UI sizing mode.")
    };
    private static string N(float value) => float.IsFinite(value) ? value.ToString("R", CultureInfo.InvariantCulture)
        : throw new InvalidOperationException("Scene UI transforms and styles must be finite.");
    private static string Color(uint value) => "#" + value.ToString("X8", CultureInfo.InvariantCulture);
}

using InnoEngine.Mathematics;
using System;
using System.Linq;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.UI.Layout;

internal static class UiLayoutPass
{
    internal static void Measure(
        UiElementInstance element,
        Func<UiElementInstance, float, (float width, float height)> measureText,
        float availableWidth
    ) {
        UiElementDefinition definition = element.definition; UiLayoutDefinition layout = definition.layout;
        availableWidth = Math.Max(0, availableWidth - HorizontalMargin(layout));
        float outerWidth = Math.Clamp(layout.widthMode == UiSizeMode.Fill ? availableWidth : layout.widthMode == UiSizeMode.Content ? Math.Min(availableWidth, layout.maxWidth) : layout.width, layout.minWidth, layout.maxWidth);
        float innerWidth = Math.Max(0, outerWidth - layout.padding * 2);
        foreach (var child in element.children)
        {
            if (definition.kind == UiElementKind.Horizontal && child.definition.layout.widthMode == UiSizeMode.Fill) continue;
            float slot = definition.kind == UiElementKind.Grid ? Math.Max(0, (innerWidth - (layout.columns - 1) * layout.gap) / layout.columns) : innerWidth;
            Measure(child, measureText, slot);
        }
        UiElementInstance[] children = element.children.Where(static child => child.visible).ToArray();
        if (definition.kind == UiElementKind.Horizontal)
        {
            int fills = children.Count(static child => child.definition.layout.widthMode == UiSizeMode.Fill);
            float fixedWidth = children.Sum(static child => child.definition.layout.widthMode == UiSizeMode.Fill ? HorizontalMargin(child.definition.layout) : OuterWidth(child));
            float share = Math.Max(0, innerWidth - fixedWidth - Math.Max(0, children.Length - 1) * layout.gap) / Math.Max(1, fills);
            foreach (var child in element.children.Where(static child => child.definition.layout.widthMode == UiSizeMode.Fill))
                Measure(child, measureText, share + HorizontalMargin(child.definition.layout));
        }
        (float width, float height) text = element.richText?.document.text.Length > 0 || element.text.Length > 0 ? measureText(element, outerWidth) : (0f, 0f);
        float contentWidth = text.width + definition.style.textPadding * 2;
        float contentHeight = text.height + definition.style.textPadding * 2;
        float visibleHeight = element.revealHeight + definition.style.textPadding * 2;
        if (children.Length > 0)
        {
            if (definition.kind == UiElementKind.Horizontal)
            {
                contentWidth = children.Sum(OuterWidth) + layout.gap * (children.Length - 1);
                contentHeight = children.Max(OuterHeight); visibleHeight = children.Max(VisibleOuterHeight);
            }
            else if (definition.kind is UiElementKind.Vertical or UiElementKind.Scroll)
            {
                contentWidth = children.Max(OuterWidth); contentHeight = children.Sum(OuterHeight) + layout.gap * (children.Length - 1);
                float cursor = 0; visibleHeight = 0;
                foreach (var child in children) { visibleHeight = Math.Max(visibleHeight, cursor + VisibleOuterHeight(child)); cursor += OuterHeight(child) + layout.gap; }
            }
            else if (definition.kind == UiElementKind.Grid)
            {
                int rows = (children.Length + layout.columns - 1) / layout.columns;
                contentWidth = children.Max(OuterWidth) * layout.columns + layout.gap * (layout.columns - 1);
                contentHeight = children.Max(OuterHeight) * rows + layout.gap * (rows - 1); visibleHeight = contentHeight;
            }
            else
            {
                contentWidth = Math.Max(contentWidth, children.Max(static child => child.definition.layout.x + OuterWidth(child)));
                contentHeight = Math.Max(contentHeight, children.Max(static child => child.definition.layout.y + OuterHeight(child)));
                visibleHeight = Math.Max(visibleHeight, children.Max(static child => child.definition.layout.y + VisibleOuterHeight(child)));
            }
        }
        element.measuredWidth = Math.Clamp(layout.widthMode == UiSizeMode.Content ? contentWidth + layout.padding * 2 : layout.widthMode == UiSizeMode.Fill ? outerWidth : layout.width, layout.minWidth, layout.maxWidth);
        element.measuredHeight = Math.Clamp(layout.heightMode == UiSizeMode.Content ? contentHeight + layout.padding * 2 : layout.height, layout.minHeight, layout.maxHeight);
        element.contentHeight = contentHeight + layout.padding * 2; element.contentVisibleHeight = visibleHeight + layout.padding * 2;
        element.visibleMeasuredHeight = layout.heightMode == UiSizeMode.Content ? Math.Clamp(element.contentVisibleHeight, layout.minHeight, layout.maxHeight) : element.measuredHeight;
    }

    internal static void Arrange(
        UiElementInstance element,
        Rect parent,
        Rect clip,
        bool positioned = false
    ) {
        UiLayoutDefinition layout = element.definition.layout;
        float availableWidth = Math.Max(0, parent.width - HorizontalMargin(layout)), availableHeight = Math.Max(0, parent.height - VerticalMargin(layout));
        float width = Math.Clamp(layout.widthMode == UiSizeMode.Fill || positioned && layout.horizontalAlignment == UiAlignment.Stretch ? availableWidth : element.measuredWidth, layout.minWidth, layout.maxWidth);
        float height = Math.Clamp(layout.heightMode == UiSizeMode.Fill || positioned && layout.verticalAlignment == UiAlignment.Stretch ? availableHeight : element.measuredHeight, layout.minHeight, layout.maxHeight);
        element.bounds = new(parent.x + layout.marginLeft + (positioned ? Offset(layout.horizontalAlignment, availableWidth, width) : layout.x + availableWidth * layout.anchorX - width * layout.pivotX),
            parent.y + layout.marginTop + (positioned ? Offset(layout.verticalAlignment, availableHeight, height) : layout.y + availableHeight * layout.anchorY - height * layout.pivotY), width, height);
        Rect.TryIntersect(clip, element.bounds, out element.clip);
        Rect inner = new(element.bounds.x + layout.padding, element.bounds.y + layout.padding, Math.Max(0, width - layout.padding * 2), Math.Max(0, height - layout.padding * 2));
        Rect childClip = layout.clip || element.definition.kind == UiElementKind.Scroll ? element.clip : clip;
        UiElementInstance[] children = element.children.Where(static child => child.visible).ToArray();
        bool horizontal = element.definition.kind == UiElementKind.Horizontal;
        bool vertical = element.definition.kind is UiElementKind.Vertical or UiElementKind.Scroll;
        float cursor = 0, gap = layout.gap;
        if (element.definition.kind == UiElementKind.Scroll)
        {
            if (element.followEnd && element.followTail && element.previousContentHeight != element.contentVisibleHeight) element.scroll = Math.Max(0, element.contentVisibleHeight - height);
            element.previousContentHeight = element.contentVisibleHeight;
            element.scroll = Math.Clamp(element.scroll, 0, Math.Max(0, element.contentHeight - height)); cursor = -element.scroll;
        }
        float fixedSize = children.Sum(child => horizontal
            ? child.definition.layout.widthMode == UiSizeMode.Fill ? HorizontalMargin(child.definition.layout) : OuterWidth(child)
            : child.definition.layout.heightMode == UiSizeMode.Fill ? VerticalMargin(child.definition.layout) : OuterHeight(child));
        int fills = children.Count(child => horizontal ? child.definition.layout.widthMode == UiSizeMode.Fill : child.definition.layout.heightMode == UiSizeMode.Fill);
        float available = Math.Max(0, (horizontal ? inner.width : inner.height) - fixedSize - Math.Max(0, children.Length - 1) * layout.gap);
        if ((horizontal || vertical) && element.definition.kind != UiElementKind.Scroll && fills == 0)
        {
            cursor = layout.flowAlignment switch { UiFlowAlignment.Center => available / 2, UiFlowAlignment.End => available, UiFlowAlignment.SpaceAround when children.Length > 0 => available / children.Length / 2, _ => 0 };
            if (layout.flowAlignment == UiFlowAlignment.SpaceBetween && children.Length > 1) gap += available / (children.Length - 1);
            else if (layout.flowAlignment == UiFlowAlignment.SpaceAround && children.Length > 0) gap += available / children.Length;
        }
        float rowHeight = children.Length == 0 ? 0 : children.Max(OuterHeight);
        for (int index = 0; index < children.Length; index++)
        {
            UiElementInstance child = children[index]; UiLayoutDefinition childLayout = child.definition.layout;
            if (horizontal || vertical)
            {
                float size = horizontal ? childLayout.widthMode == UiSizeMode.Fill && fills > 0 ? available / fills + HorizontalMargin(childLayout) : OuterWidth(child)
                    : childLayout.heightMode == UiSizeMode.Fill && fills > 0 ? available / fills + VerticalMargin(childLayout) : OuterHeight(child);
                Rect slot = horizontal ? new(inner.x + cursor, inner.y, size, inner.height) : new(inner.x, inner.y + cursor, inner.width, size);
                Arrange(child, slot, childClip, true); cursor += size + gap;
            }
            else if (element.definition.kind == UiElementKind.Grid)
            {
                float cellWidth = Math.Max(0, (inner.width - (layout.columns - 1) * layout.gap) / layout.columns);
                Arrange(child, new(inner.x + index % layout.columns * (cellWidth + layout.gap), inner.y + index / layout.columns * (rowHeight + layout.gap), cellWidth, rowHeight), childClip, true);
            }
            else Arrange(child, inner, childClip);
        }
    }
    private static float Offset(
        UiAlignment alignment,
        float available,
        float size
    ) => alignment switch { UiAlignment.Center => (available - size) / 2, UiAlignment.End => available - size, _ => 0 };
    private static float HorizontalMargin(UiLayoutDefinition layout) => layout.marginLeft + layout.marginRight;
    private static float VerticalMargin(UiLayoutDefinition layout) => layout.marginTop + layout.marginBottom;
    private static float OuterWidth(UiElementInstance element) => element.measuredWidth + HorizontalMargin(element.definition.layout);
    private static float OuterHeight(UiElementInstance element) => element.measuredHeight + VerticalMargin(element.definition.layout);
    private static float VisibleOuterHeight(UiElementInstance element) => element.visibleMeasuredHeight + VerticalMargin(element.definition.layout);
}

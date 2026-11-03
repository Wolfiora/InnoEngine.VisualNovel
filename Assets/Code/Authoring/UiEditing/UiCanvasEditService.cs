using System;
using System.Linq;
using InnoEngine.Mathematics;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Authoring.Diagnostics;

namespace Inno.VisualNovel.Authoring.UiEditing;

/// <summary>Applies canvas gestures to authored layouts while preserving anchors, pivots and automatic container ownership.</summary>
public static class UiCanvasEditService
{
    /// <summary>Captures an ordinary absolute child using bounds produced by the real VN layout pass.</summary>
    /// <param name="screen">The shared screen draft.</param>
    /// <param name="elementId">The stable declaration identity, excluding generated repeated rows.</param>
    /// <param name="bounds">The normalized design frame's actual logical bounds.</param>
    /// <returns>An immutable gesture origin without live document references.</returns>
    public static UiCanvasTransformStart Capture(
        ScreenAsset screen,
        string elementId,
        Rect bounds
    ) {
        if (!Finite(bounds.x) || !Finite(bounds.y) || !Finite(bounds.width) || !Finite(bounds.height) || bounds.width <= 0 || bounds.height <= 0)
            throw new ArgumentException("Canvas gestures require finite positive measured bounds.", nameof(bounds));
        var rows = ScreenEditService.Flatten(screen); var row = rows.Single(item => item.element.id == elementId);
        if (row.parent?.kind != UiElementKind.Panel) throw new AuthoringValidationException($"Move absolutely positioned Panel children on the canvas. For automatic layouts, edit the container, order or size.");
        for (UiElementDefinition? ancestor = row.parent; ancestor is not null; ancestor = rows.Single(item => ReferenceEquals(item.element, ancestor)).parent)
            if (ancestor.repeatBinding.Length > 0) throw new AuthoringValidationException($"Repeated rows use template layout. Edit the template properties instead of dragging an individual generated item.");
        return new(screen.root.id, row.element, row.parent.id, bounds);
    }
    /// <summary>Applies a relative move or edge resize from the frozen gesture origin without accumulating frame rounding.</summary>
    /// <param name="screen">The current detached document candidate.</param>
    /// <param name="start">The measured beginning of this gesture.</param>
    /// <param name="deltaX">The complete horizontal pointer displacement in logical canvas units.</param>
    /// <param name="deltaY">The complete vertical pointer displacement in logical canvas units.</param>
    /// <param name="edges">None moves; one or two nonopposing edges resize fixed-size axes.</param>
    /// <param name="grid">The logical snap interval, zero for unconstrained placement.</param>
    public static void Transform(
        ScreenAsset screen,
        UiCanvasTransformStart start,
        float deltaX,
        float deltaY,
        UiResizeEdges edges = UiResizeEdges.None,
        float grid = 0
    ) {
        if (!Finite(deltaX) || !Finite(deltaY) || !float.IsFinite(grid) || grid is < 0 or > 10000 || (edges & ~(UiResizeEdges.Left | UiResizeEdges.Right | UiResizeEdges.Top | UiResizeEdges.Bottom)) != 0
            || edges.HasFlag(UiResizeEdges.Left | UiResizeEdges.Right) || edges.HasFlag(UiResizeEdges.Top | UiResizeEdges.Bottom))
            throw new ArgumentException("Canvas displacement, grid or resize edges are invalid.");
        var row = ScreenEditService.Flatten(screen).Single(item => item.element.id == start.elementId);
        _ = Capture(screen, start.elementId, start.bounds);
        var layout = row.element.layout; var initial = start.layout;
        if (screen.root.id != start.root || row.parent is not { kind: UiElementKind.Panel } parent || parent.id != start.parent
            || layout.anchorX != initial.anchorX || layout.anchorY != initial.anchorY || layout.pivotX != initial.pivotX || layout.pivotY != initial.pivotY
            || layout.widthMode != initial.widthMode || layout.heightMode != initial.heightMode
            || layout.marginLeft != initial.marginLeft || layout.marginTop != initial.marginTop || layout.marginRight != initial.marginRight || layout.marginBottom != initial.marginBottom
            || layout.horizontalAlignment != initial.horizontalAlignment || layout.verticalAlignment != initial.verticalAlignment
            || layout.minWidth != initial.minWidth || layout.minHeight != initial.minHeight || layout.maxWidth != initial.maxWidth || layout.maxHeight != initial.maxHeight)
            throw new AuthoringValidationException($"Layout constraints or the parent changed. Restart the canvas gesture.");
        bool horizontal = (edges & (UiResizeEdges.Left | UiResizeEdges.Right)) != 0, vertical = (edges & (UiResizeEdges.Top | UiResizeEdges.Bottom)) != 0;
        if (horizontal && layout.widthMode != UiSizeMode.Fixed || vertical && layout.heightMode != UiSizeMode.Fixed)
            throw new AuthoringValidationException($"Choose Fixed for this dimension before resizing it. Content and Fill sizes are calculated by layout.");
        float left = start.bounds.x, top = start.bounds.y, right = left + start.bounds.width, bottom = top + start.bounds.height;
        if (edges == UiResizeEdges.None) { left = Snap(left + deltaX, grid); top = Snap(top + deltaY, grid); }
        else
        {
            if (edges.HasFlag(UiResizeEdges.Left)) left = Snap(left + deltaX, grid);
            if (edges.HasFlag(UiResizeEdges.Right)) right = Snap(right + deltaX, grid);
            if (edges.HasFlag(UiResizeEdges.Top)) top = Snap(top + deltaY, grid);
            if (edges.HasFlag(UiResizeEdges.Bottom)) bottom = Snap(bottom + deltaY, grid);
        }
        float width = horizontal ? Math.Clamp(right - left, Math.Max(1, initial.minWidth), initial.maxWidth) : start.bounds.width;
        float height = vertical ? Math.Clamp(bottom - top, Math.Max(1, initial.minHeight), initial.maxHeight) : start.bounds.height;
        if (edges.HasFlag(UiResizeEdges.Left)) left = right - width;
        if (edges.HasFlag(UiResizeEdges.Top)) top = bottom - height;
        float x = initial.x + left - start.bounds.x + (width - start.bounds.width) * initial.pivotX;
        float y = initial.y + top - start.bounds.y + (height - start.bounds.height) * initial.pivotY;
        if (!Finite(x) || !Finite(y) || !Finite(width) || !Finite(height)) throw new AuthoringValidationException($"The canvas transform exceeds the supported logical-coordinate range.");
        layout.x = x; layout.y = y;
        if (horizontal) layout.width = width;
        if (vertical) layout.height = height;
    }
    /// <summary>Aligns an absolute child against a declared reference without changing size, anchors or pivots.</summary>
    /// <param name="screen">The detached document candidate.</param>
    /// <param name="start">The measured current element.</param>
    /// <param name="reference">The explicit parent content area or complete page rectangle.</param>
    /// <param name="alignment">The edge or center to match.</param>
    public static void Align(
        ScreenAsset screen,
        UiCanvasTransformStart start,
        Rect reference,
        UiCanvasAlignment alignment
    ) {
        if (!Enum.IsDefined(alignment) || !Finite(reference.x) || !Finite(reference.y) || !Finite(reference.width) || !Finite(reference.height) || reference.width < 0 || reference.height < 0)
            throw new ArgumentException("Alignment requires a finite nonnegative reference rectangle and declared axis.");
        float x = start.bounds.x, y = start.bounds.y;
        switch (alignment)
        {
            case UiCanvasAlignment.Left: x = reference.x; break;
            case UiCanvasAlignment.HorizontalCenter: x = reference.x + (reference.width - start.bounds.width) / 2; break;
            case UiCanvasAlignment.Right: x = reference.x + reference.width - start.bounds.width; break;
            case UiCanvasAlignment.Top: y = reference.y; break;
            case UiCanvasAlignment.VerticalCenter: y = reference.y + (reference.height - start.bounds.height) / 2; break;
            case UiCanvasAlignment.Bottom: y = reference.y + reference.height - start.bounds.height; break;
        }
        Transform(screen, start, x - start.bounds.x, y - start.bounds.y);
    }
    private static float Snap(
        float value,
        float grid
    ) => grid == 0 ? value : MathF.Round(value / grid, MidpointRounding.AwayFromZero) * grid;
    private static bool Finite(float value) => float.IsFinite(value) && Math.Abs(value) <= 1e7f;
}

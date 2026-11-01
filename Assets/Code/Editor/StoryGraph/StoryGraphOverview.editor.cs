using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using InnoEngine.Graphs;
using InnoEditor.Graph;
using InnoEditor.ImGui;

namespace Inno.VisualNovel.Editor.StoryGraph;

internal sealed class StoryGraphOverview
{
    private double m_left, m_top, m_width = 1, m_height = 1;
    private (int x, int y)[] m_glyphs = [];
    private bool m_dragging;
    private const int m_columns = 80, m_rows = 48;

    internal void Cancel() => m_dragging = false;
    internal void SetNodes(IReadOnlyList<GraphCanvasNode> nodes)
    {
        Cancel();
        if (nodes.Count == 0) { m_left = m_top = 0; m_width = m_height = 1; m_glyphs = []; return; }
        m_left = nodes.Min(static node => (double)node.position.x); m_top = nodes.Min(static node => (double)node.position.y);
        double right = nodes.Max(static node => (double)node.position.x + node.size.X), bottom = nodes.Max(static node => (double)node.position.y + node.size.Y);
        m_width = Math.Max(1, right - m_left); m_height = Math.Max(1, bottom - m_top);
        m_left -= m_width * .05; m_top -= m_height * .05; m_width *= 1.1; m_height *= 1.1;
        m_glyphs = nodes.Select(node => (x: Math.Clamp((int)(((double)node.position.x + node.size.X * .5 - m_left) / m_width * m_columns), 0, m_columns - 1),
                y: Math.Clamp((int)(((double)node.position.y + node.size.Y * .5 - m_top) / m_height * m_rows), 0, m_rows - 1)))
            .Distinct().OrderBy(static point => point.y).ThenBy(static point => point.x).ToArray();
    }
    internal bool HandleInput(
        GraphCanvasState navigation,
        Vector2 origin,
        Vector2 size,
        bool allowStart
    ) {
        if (!Available(size)) { Cancel(); return false; }
        var (minimum, maximum) = Bounds(origin, size); Vector2 mouse = ImGui.GetMousePos();
        bool hovered = ImGui.IsWindowHovered() && mouse.X >= minimum.X && mouse.Y >= minimum.Y && mouse.X < maximum.X && mouse.Y < maximum.Y;
        if (!ImGui.IsWindowFocused() || ImGui.IsKeyPressed(ImGuiKey.Escape, false)) { Cancel(); return hovered; }
        if (allowStart && hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left)) m_dragging = true;
        bool captured = m_dragging;
        if (m_dragging)
        {
            if (ImGui.IsMouseButtonDown(ImGuiMouseButton.Left) || ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                var (content, extent) = Content(minimum, maximum);
                double x = m_left + Math.Clamp((mouse.X - content.X) / extent.X, 0, 1) * m_width;
                double y = m_top + Math.Clamp((mouse.Y - content.Y) / extent.Y, 0, 1) * m_height;
                Center(navigation, size, x, y);
            }
            if (!ImGui.IsMouseButtonDown(ImGuiMouseButton.Left)) Cancel();
        }
        return hovered || captured;
    }
    internal void CenterSelection(
        GraphCanvasState navigation,
        IReadOnlyList<GraphCanvasNode> nodes,
        Vector2 size,
        bool selectedOnly
    ) {
        var selected = navigation.selectedNodes.ToHashSet();
        var targets = selectedOnly ? nodes.Where(node => selected.Contains(node.id)).ToArray() : nodes.ToArray();
        if (targets.Length == 0) return;
        double left = targets.Min(static node => (double)node.position.x), top = targets.Min(static node => (double)node.position.y);
        double right = targets.Max(static node => (double)node.position.x + node.size.X), bottom = targets.Max(static node => (double)node.position.y + node.size.Y);
        Center(navigation, size, (left + right) * .5, (top + bottom) * .5);
    }
    internal void Draw(
        GraphCanvasState navigation,
        GraphCanvasLayout layout,
        Vector2 origin,
        Vector2 size
    ) {
        if (!Available(size)) return;
        var (minimum, maximum) = Bounds(origin, size); var (content, extent) = Content(minimum, maximum);
        var draw = ImGui.GetDrawList();
        draw.FilledRectangle(minimum, maximum, ImGui.ToPackedColor(new(.04f, .05f, .07f, .96f)));
        draw.Rectangle(minimum, maximum, ImGui.ToPackedColor(new(.7f, .76f, .83f, 1)), 0, 1);
        draw.PushClip(content, content + extent, true);
        try
        {
            uint ordinary = ImGui.ToPackedColor(new(.62f, .72f, .8f, 1)), selected = ImGui.ToPackedColor(new(1, .85f, .2f, 1));
            foreach (var point in m_glyphs)
            {
                Vector2 at = content + new Vector2((point.x + .5f) / m_columns * extent.X, (point.y + .5f) / m_rows * extent.Y);
                draw.FilledRectangle(at - new Vector2(1), at + new Vector2(1), ordinary);
            }
            foreach (GraphNodeId id in navigation.selectedNodes)
                if (layout.TryGetNode(id, out var node))
                {
                    Vector2 at = Project((double)node.position.x + node.size.X * .5, (double)node.position.y + node.size.Y * .5, content, extent);
                    draw.Rectangle(at - new Vector2(3), at + new Vector2(3), selected, 0, 2);
                }
            double left = -(double)navigation.pan.x / navigation.zoom, top = -(double)navigation.pan.y / navigation.zoom;
            double right = ((double)size.X - navigation.pan.x) / navigation.zoom, bottom = ((double)size.Y - navigation.pan.y) / navigation.zoom;
            if (right >= m_left && bottom >= m_top && left <= m_left + m_width && top <= m_top + m_height)
            {
                Vector2 a = Project(left, top, content, extent), b = Project(right, bottom, content, extent);
                draw.FilledRectangle(a, b, ImGui.ToPackedColor(new(.5f, .8f, 1, .12f)));
                draw.Rectangle(a, b, ImGui.ToPackedColor(new(.7f, .9f, 1, 1)), 0, 1.5f);
            }
        }
        finally { draw.PopClip(); }
    }
    private Vector2 Project(
        double x,
        double y,
        Vector2 content,
        Vector2 extent
    ) => content + new Vector2((float)Math.Clamp((x - m_left) / m_width, 0, 1) * extent.X, (float)Math.Clamp((y - m_top) / m_height, 0, 1) * extent.Y);
    private static void Center(
        GraphCanvasState navigation,
        Vector2 size,
        double x,
        double y
    ) => navigation.SetViewport(new((float)Math.Clamp(size.X * .5 - x * navigation.zoom, -float.MaxValue, float.MaxValue),
        (float)Math.Clamp(size.Y * .5 - y * navigation.zoom, -float.MaxValue, float.MaxValue)), navigation.zoom);
    private bool Available(Vector2 size) => m_glyphs.Length > 0 && size.X >= 260 && size.Y >= 180;
    private static (Vector2 minimum, Vector2 maximum) Bounds(
        Vector2 origin,
        Vector2 size
    ) => (origin + size - new Vector2(204, 132), origin + size - new Vector2(12));
    private (Vector2 content, Vector2 extent) Content(
        Vector2 minimum,
        Vector2 maximum
    ) {
        Vector2 available = maximum - minimum - new Vector2(12);
        double scale = Math.Min(available.X / m_width, available.Y / m_height);
        var extent = new Vector2(Math.Max(1, (float)(m_width * scale)), Math.Max(1, (float)(m_height * scale)));
        return ((minimum + maximum - extent) * .5f, extent);
    }
}

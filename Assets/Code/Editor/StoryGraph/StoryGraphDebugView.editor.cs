using System.Numerics;
using InnoEngine.Graphs;
using InnoEditor.Graph;
using InnoEditor.ImGui;
using Inno.VisualNovel.Editor.Localization;

namespace Inno.VisualNovel.Editor.StoryGraph;

internal sealed class StoryGraphDebugView()
{
    internal void DrawNode(
        GraphCanvasView view,
        GraphCanvasNode node,
        GraphPosition? previewPosition,
        Vector2 origin,
        Vector2 extent,
        bool breakpoint,
        bool conditional,
        bool current,
        bool paused
    ) {
        if (!breakpoint && !current) return;
        var (minimum, maximum) = view.NodeBounds(node, previewPosition);
        if (maximum.X < origin.X || maximum.Y < origin.Y || minimum.X > origin.X + extent.X || minimum.Y > origin.Y + extent.Y) return;
        var draw = ImGui.GetDrawList();
        if (current)
        {
            draw.Rectangle(minimum - new Vector2(3), maximum + new Vector2(3), ImGui.ToPackedColor(new(1, .83f, .25f, 1)), 0, 3);
            draw.Text(new(minimum.X + 8, maximum.Y - 21), ImGui.ToPackedColor(new(1, .9f, .55f, 1)),
                paused ? "Paused position" : "Execution position");
        }
        if (!breakpoint) return;
        float radius = 5 * view.zoom;
        Vector2 marker = minimum + new Vector2(node.size.X - 13, 16) * view.zoom;
        uint color = ImGui.ToPackedColor(new(1, .32f, .35f, 1));
        if (conditional) draw.Rectangle(marker - new Vector2(radius), marker + new Vector2(radius), color, 0, 2);
        else draw.FilledCircle(marker, radius, color);
    }
}

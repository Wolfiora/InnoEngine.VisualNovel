using System;
using System.Numerics;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Stage.Timeline;

namespace Inno.VisualNovel.Editor.Timeline;

internal sealed class PerformanceCurveView(
    StoryInspectorFields fields,
    IEditorHistory history
)
{
    private EditorEditGesture? m_drag;
    private int m_handle;
    internal string? mergeKey => m_drag?.mergeKey ?? fields.mergeKey;
    internal bool Draw(PerformanceCurve curve)
    {
        double x1 = curve.x1, y1 = curve.y1, x2 = curve.x2, y2 = curve.y2;
        bool changed = fields.Number("curve/x1", fields.Localize("第一个控制点 · 时间"), ref x1, 0, 1);
        changed |= fields.Number("curve/y1", fields.Localize("第一个控制点 · 进度"), ref y1, 0, 1);
        changed |= fields.Number("curve/x2", fields.Localize("第二个控制点 · 时间"), ref x2, 0, 1);
        changed |= fields.Number("curve/y2", fields.Localize("第二个控制点 · 进度"), ref y2, 0, 1);
        curve.x1 = x1; curve.y1 = y1; curve.x2 = x2; curve.y2 = y2;
        Vector2 origin = ImGui.GetCursorScreenPosition(), size = new(Math.Max(100, ImGui.GetContentRegionAvailable().X), 210);
        Vector2 top = origin + new Vector2(20), extent = size - new Vector2(40);
        Vector2 Point(double x, double y) => top + new Vector2((float)x * extent.X, (float)(1 - y) * extent.Y);
        Vector2 mouse = ImGui.GetMousePos(); bool inside = ImGui.IsWindowHovered() && mouse.X >= top.X && mouse.Y >= top.Y && mouse.X <= top.X + extent.X && mouse.Y <= top.Y + extent.Y;
        if (inside && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            float first = Vector2.Distance(mouse, Point(curve.x1, curve.y1)), second = Vector2.Distance(mouse, Point(curve.x2, curve.y2));
            if (Math.Min(first, second) <= 12) { fields.Finish(); Finish(); m_handle = first <= second ? 1 : 2; m_drag = new(history, "编辑时间曲线"); }
        }
        if (m_drag is not null)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Escape, false) || !ImGui.IsWindowFocused())
            {
                EditorHistoryResult result = m_drag.Cancel(); m_drag = null;
                if (!result.succeeded) throw new InvalidOperationException(result.message);
                ImGui.Dummy(size); return false;
            }
            if (!ImGui.IsMouseButtonDown(ImGuiMouseButton.Left)) Finish();
            else
            {
                double x = Math.Clamp((mouse.X - top.X) / extent.X, 0, 1), y = Math.Clamp(1 - (mouse.Y - top.Y) / extent.Y, 0, 1);
                if (m_handle == 1) { changed |= curve.x1 != x || curve.y1 != y; curve.x1 = x; curve.y1 = y; }
                else { changed |= curve.x2 != x || curve.y2 != y; curve.x2 = x; curve.y2 = y; }
            }
        }
        var draw = ImGui.GetDrawList(); uint grid = ImGui.ToPackedColor(new(.3f, .35f, .4f, 1));
        draw.Rectangle(top, top + extent, grid);
        draw.Line(Point(0, 0), Point(curve.x1, curve.y1), grid); draw.Line(Point(1, 1), Point(curve.x2, curve.y2), grid);
        Vector2 previous = Point(0, 0); uint accent = ImGui.ToPackedColor(new(.55f, .78f, 1, 1));
        for (int index = 1; index <= 64; index++)
        { double x = index / 64.0; Vector2 point = Point(x, PerformanceCurveEvaluator.Evaluate(curve, x)); draw.Line(previous, point, accent); previous = point; }
        draw.FilledCircle(Point(curve.x1, curve.y1), 6, ImGui.ToPackedColor(new(1, .75f, .4f, 1)));
        draw.FilledCircle(Point(curve.x2, curve.y2), 6, accent);
        ImGui.Dummy(size); EditorUiSession.current.widgets.Hint(fields.Localize("拖动控制点，或输入 0–1 的数值。横向是时间，纵向是进度；Esc 取消整次拖动。"));
        return changed;
    }
    internal void Finish() { m_drag?.Commit(); m_drag = null; }
}

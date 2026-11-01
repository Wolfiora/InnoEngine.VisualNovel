using System;
using System.Linq;
using System.Numerics;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.StageEditing;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Stage.Composition;

namespace Inno.VisualNovel.Editor.StageDesigner;

internal sealed class StageCanvasGestures(
    ResourceDocumentsModule documents,
    EditorInteractions interactions,
    StoryInspectorFields fields
)
{
    private enum Tool { Move, Scale, Rotate }
    private Tool m_tool;
    private bool m_snap = true;
    private bool m_safeArea = true;
    private double m_grid = 8;
    private double m_safeMargin = 32;
    private EditorEditGesture? m_drag, m_nudge;
    private StageCanvasTransformStart? m_start;
    private Guid m_document, m_nudgeDocument;
    private long m_revision, m_nudgeRevision;
    private string m_nudgeLayer = string.Empty;
    private Vector2 m_pointer, m_last;
    private float m_scale;
    private double m_distance, m_angle;

    internal void Finish() { m_drag?.Commit(); m_drag = null; m_start = null; FinishNudge(); }
    private void FinishNudge() { m_nudge?.Commit(); m_nudge = null; m_nudgeLayer = string.Empty; }
    internal void Toolbar(StageAsset stage)
    {
        string tool = m_tool.ToString();
        if (fields.Select("stage/canvas-tool", fields.Localize("画布工具"), ref tool,
            new[] { (nameof(Tool.Move), fields.Localize("移动图层")), (nameof(Tool.Scale), fields.Localize("等比缩放")), (nameof(Tool.Rotate), fields.Localize("旋转")) }))
        { Finish(); m_tool = Enum.Parse<Tool>(tool); }
        if (ImGui.Checkbox(fields.Label("舞台吸附"), ref m_snap)) Finish();
        ImGui.SameLine(); if (ImGui.SmallButton(fields.Caption("网格 " + m_grid + " px", fields.Localize("网格 ") + m_grid + " px"))) { Finish(); m_grid = m_grid >= 32 ? 4 : m_grid * 2; }
        ImGui.Checkbox(fields.Label("显示构图安全区"), ref m_safeArea);
        if (m_safeArea)
        {
            double maximum = Math.Max(0, (Math.Min(stage.width, stage.height) - 1) / 2);
            m_safeMargin = Math.Clamp(m_safeMargin, 0, maximum);
            fields.Number("stage/safe-margin", fields.Localize("安全参考边距（px）"), ref m_safeMargin, 0, maximum);
        }
        EditorUiSession.current.widgets.Hint(m_tool == Tool.Move ? fields.Localize("拖动图层；方向键微调 1 px，Shift 为 10 px，Alt 暂停吸附，Esc 取消当前拖动。")
            : m_tool == Tool.Scale ? fields.Localize("拖动右下角方形柄，相对作者枢轴等比缩放，保留镜像；Esc 取消。") : fields.Localize("拖动上方圆形柄旋转；吸附时每 15°，Alt 自由旋转，Esc 取消。"));
    }
    internal void Draw(
        Guid document,
        StageAsset stage,
        StageFrame frame,
        Vector2 origin,
        Vector2 size,
        float scale,
        string selected,
        Action<string> select
    ) {
        Vector2 mouse = ImGui.GetMousePos(), local = (mouse - origin) / scale;
        if (m_safeArea)
        {
            float inset = (float)m_safeMargin * scale; Vector2 a = origin + new Vector2(inset), b = origin + size - new Vector2(inset);
            var draw = ImGui.GetDrawList(); uint guide = ImGui.ToPackedColor(new(.9f, .8f, .5f, .5f)); draw.Rectangle(a, b, guide, 0, 1);
            foreach (float fraction in new[] { .25f, .5f, .75f })
            { float x = a.X + (b.X - a.X) * fraction; draw.Line(new(x, a.Y), new(x, b.Y), guide, 1); }
        }
        bool focused = ImGui.IsWindowFocused(), inside = ImGui.IsWindowHovered()
            && mouse.X >= origin.X && mouse.Y >= origin.Y && mouse.X < origin.X + size.X && mouse.Y < origin.Y + size.Y;
        if (m_nudge is not null && (!focused || document != m_nudgeDocument || selected != m_nudgeLayer
            || documents.GetRevision(document) != m_nudgeRevision || !Arrows() || Control())) FinishNudge();
        if (m_drag is not null)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Escape, false))
            {
                var result = m_drag.Cancel(); m_drag = null; m_start = null;
                if (!result.succeeded) throw new InvalidOperationException(result.message);
            }
            else if (!focused || document != m_document || selected != m_start!.layerId || documents.GetRevision(document) != m_revision
                || Math.Abs(scale - m_scale) > .0001f || !ImGui.IsMouseButtonDown(ImGuiMouseButton.Left)) Finish();
            else if (mouse != m_last)
            {
                try
                {
                    Vector2 delta = (mouse - m_pointer) / m_scale;
                    documents.Edit<StageAsset>(document, Label(), candidate =>
                    {
                        if (m_tool == Tool.Move) StageCanvasEditService.Move(candidate, m_start!, delta.X, delta.Y, m_snap && !Alt() ? m_grid : 0);
                        else if (m_tool == Tool.Scale) StageCanvasEditService.Scale(candidate, m_start!, Math.Clamp(Distance(local, m_start!) / m_distance, .001, 1000));
                        else StageCanvasEditService.Rotate(candidate, m_start!, (Angle(local, m_start!) - m_angle) * 180 / Math.PI, m_snap && !Alt() ? 15 : 0);
                    }, m_drag.mergeKey);
                    m_revision = documents.GetRevision(document); m_last = mouse;
                }
                catch { Finish(); throw; }
            }
        }
        var selectedItem = frame.items.LastOrDefault(item => item.id == "stage/" + selected);
        Vector2 handle = Vector2.Zero;
        if (selectedItem is not null)
        {
            Vector2 a = origin + selectedItem.topLeft * scale, b = origin + selectedItem.topRight * scale;
            Vector2 c = origin + selectedItem.bottomRight * scale, d = origin + selectedItem.bottomLeft * scale;
            uint color = ImGui.ToPackedColor(new(.5f, .8f, 1, 1)); var draw = ImGui.GetDrawList();
            draw.Line(a, b, color, 2); draw.Line(b, c, color, 2); draw.Line(c, d, color, 2); draw.Line(d, a, color, 2);
            var layer = stage.layers.Single(item => item.id == selected); Vector2 pivot = origin + new Vector2((float)layer.transform.x, (float)layer.transform.y) * scale;
            draw.Line(pivot - new Vector2(5, 0), pivot + new Vector2(5, 0), color, 1);
            draw.Line(pivot - new Vector2(0, 5), pivot + new Vector2(0, 5), color, 1);
            handle = c;
            if (m_tool == Tool.Rotate)
            {
                Vector2 direction = (a + b - c - d) / 4;
                handle = (a + b) / 2 + (direction.LengthSquared() > .001f ? Vector2.Normalize(direction) : new(0, -1)) * 24;
                if (size.X >= 16 && size.Y >= 16) handle = Vector2.Clamp(handle, origin + new Vector2(8), origin + size - new Vector2(8));
                draw.Line((a + b) / 2, handle, color, 1); draw.FilledCircle(handle, 5, color);
            }
            else if (m_tool == Tool.Scale) draw.Rectangle(handle - new Vector2(5), handle + new Vector2(5), color, 0, 2);
        }
        if (m_drag is null && inside && !ImGui.IsAnyItemActive() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            if (m_tool != Tool.Move && selectedItem is not null && Vector2.DistanceSquared(handle, mouse) <= 100) Begin(selected);
            else
            {
                string hit = StageCanvasEditService.HitTest(frame, local.X, local.Y);
                Finish(); fields.Finish(); select(hit);
                if (hit.Length > 0 && m_tool == Tool.Move) Begin(hit);
            }
        }
        if (focused && selectedItem is not null && m_drag is null && !ImGui.IsAnyItemActive() && !Control())
        {
            double amount = Shift() ? 10 : 1;
            double dx = (ImGui.IsKeyPressed(ImGuiKey.RightArrow) ? amount : 0) - (ImGui.IsKeyPressed(ImGuiKey.LeftArrow) ? amount : 0);
            double dy = (ImGui.IsKeyPressed(ImGuiKey.DownArrow) ? amount : 0) - (ImGui.IsKeyPressed(ImGuiKey.UpArrow) ? amount : 0);
            if (dx != 0 || dy != 0)
            {
                fields.Finish(); var start = StageCanvasEditService.Capture(stage, selected);
                m_nudge ??= new(interactions.history, "微调舞台图层"); m_nudgeDocument = document; m_nudgeLayer = selected;
                try { documents.Edit<StageAsset>(document, fields.Localize("微调舞台图层"), candidate => StageCanvasEditService.Move(candidate, start, dx, dy), m_nudge.mergeKey); m_nudgeRevision = documents.GetRevision(document); }
                catch { FinishNudge(); throw; }
            }
        }
        void Begin(string layerId)
        {
            FinishNudge(); fields.Finish(); var start = StageCanvasEditService.Capture(stage, layerId);
            double distance = Distance(local, start);
            if (m_tool != Tool.Move && distance < .001) return;
            m_start = start; m_document = document; m_revision = documents.GetRevision(document); m_pointer = m_last = mouse;
            m_scale = scale; m_distance = distance; m_angle = Angle(local, start); m_drag = new(interactions.history, Label());
        }
    }
    private string Label() => m_tool == Tool.Move ? "移动舞台图层" : m_tool == Tool.Scale ? "缩放舞台图层" : "旋转舞台图层";
    private static double Distance(
        Vector2 point,
        StageCanvasTransformStart start
    ) => Math.Sqrt(Math.Pow(point.X - start.x, 2) + Math.Pow(point.Y - start.y, 2));
    private static double Angle(
        Vector2 point,
        StageCanvasTransformStart start
    ) => Math.Atan2(point.Y - start.y, point.X - start.x);
    private static bool Alt() => ImGui.IsKeyDown(ImGuiKey.LeftAlt) || ImGui.IsKeyDown(ImGuiKey.RightAlt);
    private static bool Shift() => ImGui.IsKeyDown(ImGuiKey.LeftShift) || ImGui.IsKeyDown(ImGuiKey.RightShift);
    private static bool Control() => ImGui.IsKeyDown(ImGuiKey.LeftCtrl) || ImGui.IsKeyDown(ImGuiKey.RightCtrl) || ImGui.IsKeyDown(ImGuiKey.LeftSuper) || ImGui.IsKeyDown(ImGuiKey.RightSuper);
    private static bool Arrows() => ImGui.IsKeyDown(ImGuiKey.LeftArrow) || ImGui.IsKeyDown(ImGuiKey.RightArrow) || ImGui.IsKeyDown(ImGuiKey.UpArrow) || ImGui.IsKeyDown(ImGuiKey.DownArrow);
}

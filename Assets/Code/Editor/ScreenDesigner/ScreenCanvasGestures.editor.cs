using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using InnoEngine.Mathematics;
using UiPoint = System.Numerics.Vector2;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.UI.Documents;
using Inno.VisualNovel.Editor.Inspection;

namespace Inno.VisualNovel.Editor.ScreenDesigner;

internal sealed class ScreenCanvasGestures(
    ResourceDocumentsModule documents,
    EditorInteractions interactions,
    StoryInspectorFields fields
)
{
    private EditorEditGesture? m_drag;
    private EditorEditGesture? m_nudge;
    private Guid m_nudgeDocument;
    private string m_nudgeElement = string.Empty;
    private long m_nudgeRevision;
    private UiCanvasTransformStart? m_start;
    private UiResizeEdges m_edges;
    private UiPoint m_pointer, m_lastDelta;
    private Guid m_document;
    private long m_revision;
    private float m_scale, m_grid = 8;
    private bool m_snap = true, m_showGrid;
    private bool m_alignParent = true;
    private string m_alignment = nameof(UiCanvasAlignment.Left);

    internal void Finish() { m_drag?.Commit(); m_drag = null; m_start = null; FinishNudge(); }
    private void FinishNudge() { m_nudge?.Commit(); m_nudge = null; m_nudgeElement = string.Empty; }
    internal void Toolbar(
        Guid document,
        ScreenAsset screen,
        UiFrame frame,
        string selected,
        bool editable
    ) {
        if (ImGui.Checkbox("Snap to grid", ref m_snap)) Finish();
        ImGui.SameLine(); if (ImGui.SmallButton("Grid " + m_grid + " px")) { Finish(); m_grid = m_grid >= 32 ? 4 : m_grid * 2; }
        ImGui.SameLine(); if (ImGui.Checkbox("Show grid", ref m_showGrid)) Finish();
        var row = ScreenEditService.Flatten(screen).FirstOrDefault(item => item.element.id == selected);
        var item = frame.items.LastOrDefault(item => item.id == selected);
        if (!editable || row.parent?.kind != UiElementKind.Panel || item is null) return;
        fields.Select("canvas/alignment", "Alignment", ref m_alignment,
            new[] { (nameof(UiCanvasAlignment.Left), "Left edge"), (nameof(UiCanvasAlignment.HorizontalCenter), "Horizontal center"), (nameof(UiCanvasAlignment.Right), "Right edge"),
                (nameof(UiCanvasAlignment.Top), "Top edge"), (nameof(UiCanvasAlignment.VerticalCenter), "Vertical center"), (nameof(UiCanvasAlignment.Bottom), "Bottom edge") });
        if (ImGui.Checkbox("Relative to parent content area", ref m_alignParent)) Finish();
        ImGui.SameLine(); if (!ImGui.SmallButton("Apply alignment")) return;
        Finish(); fields.Finish();
        Rect reference = new(0, 0, frame.width, frame.height);
        if (m_alignParent)
        {
            var parent = frame.items.LastOrDefault(item => item.id == row.parent.id) ?? throw new InvalidOperationException("The parent has no available design preview. Check its visibility and sample data.");
            float padding = row.parent.layout.padding;
            reference = new(parent.bounds.x + padding, parent.bounds.y + padding, Math.Max(0, parent.bounds.width - 2 * padding), Math.Max(0, parent.bounds.height - 2 * padding));
        }
        var start = UiCanvasEditService.Capture(screen, selected, item.bounds); var alignment = Enum.Parse<UiCanvasAlignment>(m_alignment);
        documents.Edit<ScreenAsset>(document, "Align UI control", candidate => UiCanvasEditService.Align(candidate, start, reference, alignment));
    }
    internal void Draw(
        Guid document,
        ScreenAsset screen,
        UiFrame frame,
        UiPoint origin,
        UiPoint size,
        float scale,
        string selected,
        Action<string> select,
        bool editable
    ) {
        if (m_showGrid) Grid(frame, origin, scale);
        UiPoint mouse = ImGui.GetMousePos(), local = (mouse - origin) / scale;
        bool inside = mouse.X >= origin.X && mouse.Y >= origin.Y && mouse.X < origin.X + size.X && mouse.Y < origin.Y + size.Y && ImGui.IsWindowHovered();
        bool focused = ImGui.IsWindowFocused(ImGuiFocusedFlags.None);
        if (m_nudge is not null && (!editable || !focused || m_nudgeDocument != document || m_nudgeElement != selected
            || documents.GetRevision(document) != m_nudgeRevision || !Arrows() || Control() || Alt())) FinishNudge();
        if (m_drag is not null)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Escape, false))
            {
                var result = m_drag.Cancel(); m_drag = null; m_start = null;
                if (!result.succeeded) throw new InvalidOperationException(result.message);
            }
            else if (!editable || !focused || m_document != document || m_start?.elementId != selected || Math.Abs(scale - m_scale) > .0001f
                || documents.GetRevision(document) != m_revision || !ImGui.IsMouseButtonDown(ImGuiMouseButton.Left)) Finish();
            else
            {
                UiPoint delta = (mouse - m_pointer) / m_scale;
                if (delta != m_lastDelta)
                {
                    try
                    {
                        documents.Edit<ScreenAsset>(document, Label(m_edges), candidate => UiCanvasEditService.Transform(candidate, m_start!, delta.X, delta.Y, m_edges,
                            m_snap && !Alt() ? m_grid : 0), m_drag.mergeKey);
                        m_revision = documents.GetRevision(document); m_lastDelta = delta;
                    }
                    catch { Finish(); throw; }
                }
            }
        }
        var selectedItem = frame.items.LastOrDefault(item => item.id == selected);
        var selectedDefinition = ScreenEditService.Flatten(screen).FirstOrDefault(item => item.element.id == selected);
        var handles = selectedItem is not null && selectedDefinition.parent?.kind == UiElementKind.Panel
            ? Handles(selectedDefinition.element, selectedItem, origin, scale).ToArray() : [];
        if (editable && m_drag is null && inside && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            var handle = handles.FirstOrDefault(handle => Math.Abs(mouse.X - handle.point.X) <= 7 && Math.Abs(mouse.Y - handle.point.Y) <= 7);
            if (handle.edges != UiResizeEdges.None) Begin(selectedItem!, handle.edges);
            else
            {
                var hit = frame.items.LastOrDefault(item => item.bounds.Contains(local.X, local.Y) && item.clip.Contains(local.X, local.Y));
                if (hit is not null)
                {
                    var row = ScreenEditService.Flatten(screen).Where(item => item.element.id == hit.id || hit.id.EndsWith("/" + item.element.id, StringComparison.Ordinal))
                        .OrderByDescending(static item => item.element.id.Length).FirstOrDefault();
                    if (row.element is not null)
                    {
                        Finish(); fields.Finish(); select(row.element.id);
                        if (row.parent?.kind == UiElementKind.Panel && hit.id == row.element.id) Begin(hit, UiResizeEdges.None);
                    }
                }
            }
        }
        if (editable && focused && !ImGui.IsAnyItemActive() && m_drag is null && !Control() && !Alt() && selectedItem is not null && selectedDefinition.parent?.kind == UiElementKind.Panel)
        {
            float amount = Shift() ? 10 : 1;
            float dx = (ImGui.IsKeyPressed(ImGuiKey.RightArrow) ? amount : 0) - (ImGui.IsKeyPressed(ImGuiKey.LeftArrow) ? amount : 0);
            float dy = (ImGui.IsKeyPressed(ImGuiKey.DownArrow) ? amount : 0) - (ImGui.IsKeyPressed(ImGuiKey.UpArrow) ? amount : 0);
            if (dx != 0 || dy != 0)
            {
                var start = UiCanvasEditService.Capture(screen, selected, selectedItem.bounds);
                fields.Finish();
                m_nudge ??= new EditorEditGesture(interactions.history, "Nudge UI control"); m_nudgeDocument = document; m_nudgeElement = selected;
                try { documents.Edit<ScreenAsset>(document, "Nudge UI control", candidate => UiCanvasEditService.Transform(candidate, start, dx, dy), m_nudge.mergeKey); m_nudgeRevision = documents.GetRevision(document); }
                catch { FinishNudge(); throw; }
            }
        }
        uint color = ImGui.ToPackedColor(new(.5f, .8f, 1, 1));
        foreach (var item in frame.items.Where(item => item.id == selected || item.id.EndsWith("/" + selected, StringComparison.Ordinal)))
            ImGui.GetDrawList().Rectangle(origin + new UiPoint(item.bounds.x, item.bounds.y) * scale,
                origin + new UiPoint(item.bounds.x + item.bounds.width, item.bounds.y + item.bounds.height) * scale, color, 0, 2);
        if (editable) foreach (var handle in handles) ImGui.GetDrawList().Rectangle(handle.point - new UiPoint(4), handle.point + new UiPoint(4), color, 0, 2);

        void Begin(
            UiDrawItem item,
            UiResizeEdges edges
        ) {
            FinishNudge(); fields.Finish();
            m_start = UiCanvasEditService.Capture(screen, item.id, item.bounds); m_edges = edges; m_pointer = mouse; m_lastDelta = UiPoint.Zero;
            m_document = document; m_revision = documents.GetRevision(document); m_scale = scale;
            m_drag = new(interactions.history, Label(edges));
        }
    }
    private void Grid(
        UiFrame frame,
        UiPoint origin,
        float scale
    ) {
        float step = m_grid * Math.Max(1, MathF.Ceiling(8 / (m_grid * scale)));
        step *= Math.Max(1, MathF.Ceiling(Math.Max(frame.width, frame.height) / (step * 200)));
        uint color = ImGui.ToPackedColor(new(.6f, .7f, .8f, .16f));
        for (float x = step; x < frame.width; x += step) ImGui.GetDrawList().Line(origin + new UiPoint(x * scale, 0), origin + new UiPoint(x * scale, frame.height * scale), color, 1);
        for (float y = step; y < frame.height; y += step) ImGui.GetDrawList().Line(origin + new UiPoint(0, y * scale), origin + new UiPoint(frame.width * scale, y * scale), color, 1);
    }
    private static IEnumerable<(UiPoint point, UiResizeEdges edges)> Handles(
        UiElementDefinition element,
        UiDrawItem item,
        UiPoint origin,
        float scale
    ) {
        bool x = element.layout.widthMode == UiSizeMode.Fixed, y = element.layout.heightMode == UiSizeMode.Fixed;
        UiPoint a = origin + new UiPoint(item.bounds.x, item.bounds.y) * scale, b = a + new UiPoint(item.bounds.width, item.bounds.height) * scale, middle = (a + b) / 2;
        if (x) { yield return (new(a.X, middle.Y), UiResizeEdges.Left); yield return (new(b.X, middle.Y), UiResizeEdges.Right); }
        if (y) { yield return (new(middle.X, a.Y), UiResizeEdges.Top); yield return (new(middle.X, b.Y), UiResizeEdges.Bottom); }
        if (x && y)
        {
            yield return (a, UiResizeEdges.Left | UiResizeEdges.Top); yield return (new(b.X, a.Y), UiResizeEdges.Right | UiResizeEdges.Top);
            yield return (b, UiResizeEdges.Right | UiResizeEdges.Bottom); yield return (new(a.X, b.Y), UiResizeEdges.Left | UiResizeEdges.Bottom);
        }
    }
    private static bool Shift() => ImGui.IsKeyDown(ImGuiKey.LeftShift) || ImGui.IsKeyDown(ImGuiKey.RightShift);
    private static bool Control() => ImGui.IsKeyDown(ImGuiKey.LeftCtrl) || ImGui.IsKeyDown(ImGuiKey.RightCtrl) || ImGui.IsKeyDown(ImGuiKey.LeftSuper) || ImGui.IsKeyDown(ImGuiKey.RightSuper);
    private static bool Alt() => ImGui.IsKeyDown(ImGuiKey.LeftAlt) || ImGui.IsKeyDown(ImGuiKey.RightAlt);
    private static bool Arrows() => ImGui.IsKeyDown(ImGuiKey.LeftArrow) || ImGui.IsKeyDown(ImGuiKey.RightArrow) || ImGui.IsKeyDown(ImGuiKey.UpArrow) || ImGui.IsKeyDown(ImGuiKey.DownArrow);
    private static string Label(UiResizeEdges edges) => edges == UiResizeEdges.None ? "Move UI control" : "Resize UI control";
}

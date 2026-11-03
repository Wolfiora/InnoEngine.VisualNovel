using System;
using System.Collections.Generic;
using System.Numerics;
using InnoEngine.Graphs;
using InnoEditor.ImGui;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.ScriptProjection;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Editor.Localization;

namespace Inno.VisualNovel.Editor.ScriptEditor;

internal sealed class ScriptCardDragView()
{
    private const string m_payloadType = "INNO_VN_SCRIPT_CARD";
    private Guid m_owner = Guid.NewGuid();
    private Payload? m_payload;
    private readonly record struct Payload(Guid owner, Guid document, long revision, int row);

    internal void Reset() { m_owner = Guid.NewGuid(); m_payload = null; }

    internal (string node, GraphEdgeId destination)? DrawRow(
        Guid document,
        long revision,
        StoryDraftState draft,
        IReadOnlyList<ScriptRow> rows,
        int rowIndex,
        StoryReorderService reorder
    ) {
        var widgets = EditorUiSession.current.widgets;
        Vector2 minimum = ImGui.GetItemMinimum(), maximum = ImGui.GetItemMaximum();
        if (!ImGui.IsMouseButtonDown(ImGuiMouseButton.Left)) m_payload = null;
        if (rows[rowIndex].incoming == 1 && rows[rowIndex].targets.Count == 1)
            widgets.DragDropSource(m_payloadType, () =>
            {
                m_payload ??= new(m_owner, document, revision, rowIndex); return m_payload.Value;
            }, () => ImGui.Text(rows[rowIndex].nodeId));
        bool delivered = widgets.DragDropTarget(m_payloadType, out Payload payload, out bool preview, false);
        if ((!delivered && !preview) || !Valid(payload, document, revision, rows.Count)) return null;
        bool after = ImGui.GetMousePos().Y >= (minimum.Y + maximum.Y) * .5f;
        try
        {
            var edge = reorder.DropConnection(draft, new(rows[payload.row].nodeId), new(rows[rowIndex].nodeId), after);
            float y = after ? maximum.Y : minimum.Y;
            ImGui.GetDrawList().Line(new(minimum.X, y), new(maximum.X, y), ImGui.ToPackedColor(new(.5f, .8f, 1, 1)), 2);
            widgets.Hint("Release to move along the actual connections. Review changes before crossing branches or fragments.");
            return delivered ? (rows[payload.row].nodeId, edge) : null;
        }
        catch (InvalidOperationException error) { if (preview) widgets.Hint((error).Message); return null; }
    }

    internal (string node, GraphEdgeId destination)? DrawConnection(
        Guid document,
        long revision,
        IReadOnlyList<ScriptRow> rows,
        GraphEdgeRecord edge
    ) {
        var widgets = EditorUiSession.current.widgets;
        bool delivered = widgets.DragDropTarget(m_payloadType, out Payload payload, out bool preview, false);
        if ((!delivered && !preview) || !Valid(payload, document, revision, rows.Count)) return null;
        string source = rows[payload.row].nodeId;
        if (edge.input.nodeId.value == source || edge.output.nodeId.value == source) return null;
        ImGui.GetDrawList().Rectangle(ImGui.GetItemMinimum(), ImGui.GetItemMaximum(), ImGui.ToPackedColor(new(.5f, .8f, 1, 1)), 0, 2);
        widgets.Hint("Insert this instruction on this execution connection. Review changes for complex control flow.");
        return delivered ? (source, edge.id) : null;
    }

    private bool Valid(
        Payload payload,
        Guid document,
        long revision,
        int rows
    ) => payload.owner == m_owner && payload.document == document && payload.revision == revision && payload.row >= 0 && payload.row < rows;
}

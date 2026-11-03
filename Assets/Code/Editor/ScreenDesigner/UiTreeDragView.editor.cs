using System;
using System.Collections.Generic;
using System.Numerics;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Editor.Localization;

namespace Inno.VisualNovel.Editor.ScreenDesigner;

internal sealed class UiTreeDragView()
{
    private const string m_payloadType = "INNO_VN_UI_TREE";
    private readonly Guid m_owner = Guid.NewGuid();
    private Payload? m_payload;
    private readonly record struct Payload(Guid owner, Guid document, long revision, int row);

    internal UiHierarchyDropTarget? DrawRow(
        Guid document,
        long revision,
        ScreenAsset screen,
        IReadOnlyList<(UiElementDefinition element, UiElementDefinition? parent, int depth)> rows,
        int rowIndex
    ) {
        var row = rows[rowIndex];
        var widgets = EditorUiSession.current.widgets;
        Vector2 minimum = ImGui.GetItemMinimum(), maximum = ImGui.GetItemMaximum();
        if (!ImGui.IsMouseButtonDown(ImGuiMouseButton.Left)) m_payload = null;
        if (row.parent is not null)
            widgets.DragDropSource(m_payloadType, () =>
            {
                m_payload ??= new(m_owner, document, revision, rowIndex); return m_payload.Value;
            }, () => ImGui.Text(row.element.kind + " · " + row.element.id));
        bool delivered = widgets.DragDropTarget(m_payloadType, out Payload payload, out bool preview, false);
        if ((!delivered && !preview) || payload.owner != m_owner || payload.document != document || payload.revision != revision || payload.row < 0 || payload.row >= rows.Count) return null;
        float fraction = (ImGui.GetMousePos().Y - minimum.Y) / Math.Max(1, maximum.Y - minimum.Y);
        EditorDropPlacement placement = fraction < .25 ? EditorDropPlacement.Before : fraction > .75 ? EditorDropPlacement.After : EditorDropPlacement.Into;
        try
        {
            var destination = UiHierarchyDropService.Describe(screen, rows[payload.row].element.id, row.element.id, placement);
            uint accent = ImGui.ToPackedColor(new(0.5f, .8f, 1, 1)); var draw = ImGui.GetDrawList();
            if (placement == EditorDropPlacement.Into) draw.Rectangle(minimum, maximum, accent, 0, 2);
            else { float y = placement == EditorDropPlacement.Before ? minimum.Y : maximum.Y; draw.Line(new(minimum.X, y), new(maximum.X, y), accent, 2); }
            widgets.Hint(destination.sourceId + " → " + destination.parentId + " [" + (destination.index + 1) + "]"
                + (destination.changesParent ? " · Release to review the new parent's layout and clipping" : " · Release to reorder siblings")
                + (destination.changesRowContext ? " · item.* data context will change" : ""));
            return delivered ? destination : null;
        }
        catch (InvalidOperationException failure) { if (preview) widgets.Hint((failure).Message); return null; }
    }
}

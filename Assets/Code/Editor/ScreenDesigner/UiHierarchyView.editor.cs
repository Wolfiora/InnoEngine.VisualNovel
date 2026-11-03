using System;
using System.Linq;
using InnoEditor.ImGui;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Model.Interface;

namespace Inno.VisualNovel.Editor.ScreenDesigner;

internal sealed class UiHierarchyView(StoryInspectorFields fields)
{
    private string m_selected = string.Empty, m_parent = string.Empty;
    private int m_index;
    private bool m_proposed;

    internal void Reset() { m_selected = m_parent = string.Empty; m_index = 0; m_proposed = false; }
    internal void Propose(UiHierarchyDropTarget target)
    { m_selected = target.sourceId; m_parent = target.parentId; m_index = target.index; m_proposed = true; }

    internal bool Draw(
        ScreenAsset screen,
        string selected,
        out string parentId,
        out int index
    ) {
        parentId = string.Empty; index = 0;
        var rows = ScreenEditService.Flatten(screen);
        var source = rows.Single(item => item.element.id == selected);
        if (source.parent is null) return false;
        int current = Array.IndexOf(source.parent.children, source.element);
        ImGui.SeparatorText("Hierarchy and order");
        ImGui.BeginDisabled(current == 0);
        try { if (ImGui.Button("Move up##ui-order")) { parentId = source.parent.id; index = current - 1; return true; } }
        finally { ImGui.EndDisabled(); }
        ImGui.SameLine(); ImGui.BeginDisabled(current == source.parent.children.Length - 1);
        try { if (ImGui.Button("Move down##ui-order")) { parentId = source.parent.id; index = current + 1; return true; } }
        finally { ImGui.EndDisabled(); }
        if (m_proposed) EditorUiSession.current.widgets.Hint("The drop selected a new parent. Review layout, clipping and item.* context below before applying; the screen has not changed.");
        if (!ImGui.CollapsingHeader("Change parent / position", m_proposed ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None)) return false;
        var targets = ScreenEditService.ReparentTargets(screen, selected).ToHashSet(StringComparer.Ordinal);
        if (m_selected != selected || !targets.Contains(m_parent))
        { m_selected = selected; m_parent = source.parent.id; m_index = current; m_proposed = false; }
        if (fields.Select(selected + "/new-parent", "Target parent", ref m_parent,
            rows.Where(row => targets.Contains(row.element.id)).Select(static row => (row.element.id, row.element.kind + " · " + row.element.id))))
            m_index = rows.Single(row => row.element.id == m_parent).element.children.Count(child => child.id != selected);
        int count = rows.Single(row => row.element.id == m_parent).element.children.Count(child => child.id != selected);
        double slot = Math.Clamp(m_index, 0, count) + 1;
        fields.Number(selected + "/new-index", "Target position (starting at 1)", ref slot, 1, count + 1); m_index = (int)Math.Round(slot) - 1;
        EditorUiSession.current.widgets.Hint(source.parent.id + " [" + (current + 1) + "] → " + m_parent + " [" + (m_index + 1) + "]\nControl IDs, local positions, actions and bindings are preserved. The new parent may change stacking, clipping and item.* row context. These bindings will not be guessed or rewritten.");
        ImGui.BeginDisabled(m_parent == source.parent.id && m_index == current);
        try
        {
            if (!ImGui.Button("Apply hierarchy change")) return false;
            parentId = m_parent; index = m_index; m_proposed = false; return true;
        }
        finally { ImGui.EndDisabled(); }
    }
}

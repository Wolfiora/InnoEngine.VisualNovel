using System;
using System.Collections.Generic;
using System.Linq;
using InnoEditor.ImGui;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.StageEditing;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Model.Stages;

namespace Inno.VisualNovel.Editor.StageDesigner;

internal sealed class StageLayerBatchView(StoryInspectorFields fields)
{
    private readonly HashSet<string> m_selected = new(StringComparer.Ordinal);
    private Guid m_document;
    private string m_search = string.Empty;
    private int m_page;
    private double m_x, m_y, m_rotation, m_scale = 1, m_opacity = 1;
    private bool m_setOpacity, m_setVisibility, m_visible = true, m_setColor, m_setGroup;
    private uint m_color = 0xFFFFFFFF;
    private StageLayerGroup m_group;
    internal bool Draw(
        ResourceDocumentsModule documents,
        Guid document,
        StageAsset stage,
        Action finish
    ) {
        if (m_document != document) { m_selected.Clear(); m_document = document; m_search = string.Empty; m_page = 0; }
        m_selected.IntersectWith(stage.layers.Select(static layer => layer.id));
        if (!ImGui.CollapsingHeader("Batch edit stage layers")) return false;
        if (fields.Text("stage-batch/search", "Filter batch layers", ref m_search)) m_page = 0;
        var shown = stage.layers.Where(layer => layer.name.Contains(m_search, StringComparison.OrdinalIgnoreCase) || layer.id.Contains(m_search, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (ImGui.Button("Select all filtered layers")) foreach (StageLayer layer in shown) m_selected.Add(layer.id);
        ImGui.SameLine(); if (ImGui.Button("Clear batch selection")) m_selected.Clear();
        ImGui.Text(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0} selected layers · {1} matching layers", m_selected.Count, shown.Length));
        int pages = Math.Max(1, (shown.Length + 39) / 40); m_page = Math.Clamp(m_page, 0, pages - 1);
        foreach (StageLayer layer in shown.Skip(m_page * 40).Take(40))
        {
            bool selected = m_selected.Contains(layer.id);
            if (ImGui.Checkbox(layer.name + " · " + layer.id + "###stage-batch/" + layer.id, ref selected))
            { if (selected) m_selected.Add(layer.id); else m_selected.Remove(layer.id); }
        }
        if (m_page > 0 && ImGui.SmallButton("Previous##stage-batch")) m_page--;
        ImGui.SameLine(); ImGui.Text(string.Format(System.Globalization.CultureInfo.InvariantCulture,"Page {0} of {1}", m_page + 1, pages));
        ImGui.SameLine(); if (m_page + 1 < pages && ImGui.SmallButton("Next##stage-batch")) m_page++;
        fields.Number("stage-batch/x", "Batch horizontal offset", ref m_x, -1_000_000, 1_000_000);
        fields.Number("stage-batch/y", "Batch vertical offset", ref m_y, -1_000_000, 1_000_000);
        fields.Number("stage-batch/scale", "Batch scale factor (preserve mirroring)", ref m_scale, .001, 1000);
        fields.Number("stage-batch/rotation", "Batch rotation offset (degrees)", ref m_rotation, -1_000_000, 1_000_000);
        fields.Toggle("stage-batch/set-alpha", "Override selected opacity", ref m_setOpacity);
        if (m_setOpacity) fields.Number("stage-batch/alpha", "Shared opacity", ref m_opacity, 0, 1);
        fields.Toggle("stage-batch/set-visible", "Override selected visibility", ref m_setVisibility);
        if (m_setVisibility) fields.Toggle("stage-batch/visible", "Shared visibility", ref m_visible);
        fields.Toggle("stage-batch/set-color", "Override selected color", ref m_setColor);
        if (m_setColor) fields.Color("stage-batch/color", "Shared layer color", ref m_color);
        fields.Toggle("stage-batch/set-band", "Override selected paint band", ref m_setGroup);
        if (m_setGroup) fields.SelectEnum("stage-batch/band", "Shared paint band", ref m_group);
        EditorUiSession.current.widgets.Hint("Position and rotation are relative offsets; scaling preserves each layer's mirroring and pivot. Only enabled appearance overrides apply. Selection includes layers on other filter pages. Undo restores the whole change.");
        bool empty = m_x == 0 && m_y == 0 && m_scale == 1 && m_rotation == 0 && !m_setOpacity && !m_setVisibility && !m_setColor && !m_setGroup;
        ImGui.BeginDisabled(m_selected.Count == 0 || empty);
        try
        {
            if (!ImGui.Button("Apply to all selected layers")) return false;
            finish(); string[] ids = m_selected.ToArray();
            var change = new StageLayerBatchChange { offset = new((float)m_x, (float)m_y), scale = m_scale, rotation = m_rotation,
                opacity = m_setOpacity ? m_opacity : null, visible = m_setVisibility ? m_visible : null, color = m_setColor ? m_color : null, group = m_setGroup ? m_group : null };
            documents.Edit<StageAsset>(document, "Batch edit stage layers", candidate => StageLayerBatchEditService.Apply(candidate, ids, change));
            return true;
        }
        finally { ImGui.EndDisabled(); }
    }
}

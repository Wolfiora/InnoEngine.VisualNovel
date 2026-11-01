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
        if (!ImGui.CollapsingHeader(fields.Label("批量修改舞台图层"))) return false;
        if (fields.Text("stage-batch/search", "筛选批量图层", ref m_search)) m_page = 0;
        var shown = stage.layers.Where(layer => layer.name.Contains(m_search, StringComparison.OrdinalIgnoreCase) || layer.id.Contains(m_search, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (ImGui.Button(fields.Label("勾选所有筛选结果"))) foreach (StageLayer layer in shown) m_selected.Add(layer.id);
        ImGui.SameLine(); if (ImGui.Button(fields.Label("清空批量勾选"))) m_selected.Clear();
        ImGui.Text(fields.Format("已勾选 {0} 层 · 筛选结果 {1} 层", m_selected.Count, shown.Length));
        int pages = Math.Max(1, (shown.Length + 39) / 40); m_page = Math.Clamp(m_page, 0, pages - 1);
        foreach (StageLayer layer in shown.Skip(m_page * 40).Take(40))
        {
            bool selected = m_selected.Contains(layer.id);
            if (ImGui.Checkbox(layer.name + " · " + layer.id + "###stage-batch/" + layer.id, ref selected))
            { if (selected) m_selected.Add(layer.id); else m_selected.Remove(layer.id); }
        }
        if (m_page > 0 && ImGui.SmallButton(fields.Label("上一页##stage-batch"))) m_page--;
        ImGui.SameLine(); ImGui.Text(fields.Format("第 {0}/{1} 页", m_page + 1, pages));
        ImGui.SameLine(); if (m_page + 1 < pages && ImGui.SmallButton(fields.Label("下一页##stage-batch"))) m_page++;
        fields.Number("stage-batch/x", "批量横向偏移", ref m_x, -1_000_000, 1_000_000);
        fields.Number("stage-batch/y", "批量纵向偏移", ref m_y, -1_000_000, 1_000_000);
        fields.Number("stage-batch/scale", "批量缩放倍数（保留镜像）", ref m_scale, .001, 1000);
        fields.Number("stage-batch/rotation", "批量旋转偏移（度）", ref m_rotation, -1_000_000, 1_000_000);
        fields.Toggle("stage-batch/set-alpha", "覆盖所选不透明度", ref m_setOpacity);
        if (m_setOpacity) fields.Number("stage-batch/alpha", "统一不透明度", ref m_opacity, 0, 1);
        fields.Toggle("stage-batch/set-visible", "覆盖所选显隐", ref m_setVisibility);
        if (m_setVisibility) fields.Toggle("stage-batch/visible", "统一可见状态", ref m_visible);
        fields.Toggle("stage-batch/set-color", "覆盖所选颜色", ref m_setColor);
        if (m_setColor) fields.Color("stage-batch/color", "统一图层颜色", ref m_color);
        fields.Toggle("stage-batch/set-band", "覆盖所选合成分组", ref m_setGroup);
        if (m_setGroup) fields.SelectEnum("stage-batch/band", "统一合成分组", ref m_group);
        EditorUiSession.current.widgets.Hint(fields.Localize("位置与旋转相对偏移；缩放保留各层镜像和枢轴。仅开启的外观覆盖生效，勾选包含其他筛选页的图层。所有更改可一起撤销。"));
        bool empty = m_x == 0 && m_y == 0 && m_scale == 1 && m_rotation == 0 && !m_setOpacity && !m_setVisibility && !m_setColor && !m_setGroup;
        ImGui.BeginDisabled(m_selected.Count == 0 || empty);
        try
        {
            if (!ImGui.Button(fields.Label("应用到全部勾选图层"))) return false;
            finish(); string[] ids = m_selected.ToArray();
            var change = new StageLayerBatchChange { offset = new((float)m_x, (float)m_y), scale = m_scale, rotation = m_rotation,
                opacity = m_setOpacity ? m_opacity : null, visible = m_setVisibility ? m_visible : null, color = m_setColor ? m_color : null, group = m_setGroup ? m_group : null };
            documents.Edit<StageAsset>(document, fields.Localize("批量编辑舞台图层"), candidate => StageLayerBatchEditService.Apply(candidate, ids, change));
            return true;
        }
        finally { ImGui.EndDisabled(); }
    }
}

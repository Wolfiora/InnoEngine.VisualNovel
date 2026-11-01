using System;
using System.Linq;
using InnoEditor.ImGui;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Editor.Variables;

internal sealed class VariableCatalogView(StoryInspectorFields fields)
{
    private string m_search = string.Empty;
    private int m_page;
    private const int m_pageSize = 40;
    internal string Draw(
        VariableDefinition[] variables,
        string selected
    ) {
        if (fields.Text("variables/catalog-search", fields.Localize("搜索身份、名称、分组或说明"), ref m_search)) m_page = 0;
        var visible = variables.Where(variable => new[] { variable.id, variable.name, variable.group, variable.description }
            .Any(value => value.Contains(m_search, StringComparison.OrdinalIgnoreCase))).OrderBy(static variable => variable.group, StringComparer.Ordinal).ToArray();
        int pages = Math.Max(1, (visible.Length + m_pageSize - 1) / m_pageSize); m_page = Math.Clamp(m_page, 0, pages - 1);
        ImGui.BeginDisabled(m_page == 0);
        try { if (ImGui.SmallButton(fields.Label("上一页变量"))) m_page--; } finally { ImGui.EndDisabled(); }
        ImGui.SameLine(); ImGui.BeginDisabled(m_page == pages - 1);
        try { if (ImGui.SmallButton(fields.Label("下一页变量"))) m_page++; } finally { ImGui.EndDisabled(); }
        ImGui.Text(visible.Length + fields.Localize(" 个匹配变量 · ") + (m_page + 1) + "/" + pages);
        string? group = null;
        foreach (var variable in visible.Skip(m_page * m_pageSize).Take(m_pageSize))
        {
            if (group != variable.group) { group = variable.group; ImGui.SeparatorText(group.Length == 0 ? fields.Localize("未分组") : group); }
            if (ImGui.Selectable(variable.name + " · " + variable.id + " · " + variable.initialValue.kind + " · " + variable.scope + "##variable/" + variable.id, selected == variable.id))
            { fields.Finish(); selected = variable.id; }
        }
        return selected;
    }
}

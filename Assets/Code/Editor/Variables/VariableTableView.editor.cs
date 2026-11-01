using System;
using System.Linq;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Editor.Variables;

internal sealed class VariableTableView(
    StoryInspectorFields fields,
    ResourceDocumentsModule resources,
    EditorInteractions interactions
)
{
    private readonly VariableDefinitionFields m_definition = new(fields);
    private readonly VariableCatalogView m_catalog = new(fields);
    private readonly StoryVariableLibraryView m_library = new(fields, resources, interactions);
    private string m_newId = "variable";
    private string m_selected = string.Empty;

    internal void Draw(
        StoryDocumentsModule documents,
        StoryDraftState draft,
        Action<string> selectNode
    ) {
        var edits = new VariableEditService(documents.codec);
        if (m_library.Draw(documents, draft, selectNode)) return;
        ImGui.Text(fields.Localize("变量 ID 在创建后保持稳定；对白中使用 {ID} 插值。"));
        fields.Text("new-variable-id", fields.Localize("新变量 ID"), ref m_newId);
        if (ImGui.Button(fields.Label("添加变量 / Add variable")))
        {
            fields.Finish();
            documents.Edit(documents.activeDocumentId, fields.Localize("添加变量"), state => edits.Add(state,
                new() { id = m_newId, name = m_newId, initialValue = NovelValue.Boolean(false) }));
            m_selected = m_newId;
            return;
        }
        m_selected = m_catalog.Draw(draft.variables, m_selected);
        VariableDefinition? variable = draft.variables.FirstOrDefault(item => item.id == m_selected);
        if (variable is null) return;
        ImGui.SeparatorText(variable.id);
        if (m_definition.Draw(variable))
            documents.Edit(documents.activeDocumentId, fields.Localize("编辑变量声明"), state => edits.Update(state, variable), fields.mergeKey);
        var references = edits.FindUsages(draft, variable.id);
        ImGui.SeparatorText(fields.Localize("引用位置"));
        foreach (string nodeId in references)
            if (ImGui.Selectable(nodeId)) { fields.Finish(); selectNode(nodeId); }
        ImGui.BeginDisabled(references.Count > 0);
        try
        {
            if (ImGui.Button(fields.Label("删除未引用变量 / Remove unused")))
            {
                fields.Finish();
                documents.Edit(documents.activeDocumentId, fields.Localize("删除变量"), state => edits.Remove(state, variable.id));
                m_selected = string.Empty;
            }
        }
        finally { ImGui.EndDisabled(); }
    }
}

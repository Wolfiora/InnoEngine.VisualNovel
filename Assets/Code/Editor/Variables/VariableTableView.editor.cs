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
        ImGui.Text("Variable IDs stay stable after creation; use {ID} for dialogue interpolation.");
        fields.Text("new-variable-id", "New variable ID", ref m_newId);
        if (ImGui.Button("Add variable"))
        {
            fields.Finish();
            documents.Edit(documents.activeDocumentId, "Add variable", state => edits.Add(state,
                new() { id = m_newId, name = m_newId, initialValue = NovelValue.Boolean(false) }));
            m_selected = m_newId;
            return;
        }
        m_selected = m_catalog.Draw(draft.variables, m_selected);
        VariableDefinition? variable = draft.variables.FirstOrDefault(item => item.id == m_selected);
        if (variable is null) return;
        ImGui.SeparatorText(variable.id);
        if (m_definition.Draw(variable))
            documents.Edit(documents.activeDocumentId, "Edit variable declaration", state => edits.Update(state, variable), fields.mergeKey);
        var references = edits.FindUsages(draft, variable.id);
        ImGui.SeparatorText("Reference locations");
        foreach (string nodeId in references)
            if (ImGui.Selectable(nodeId)) { fields.Finish(); selectNode(nodeId); }
        ImGui.BeginDisabled(references.Count > 0);
        try
        {
            if (ImGui.Button("Remove unused variable"))
            {
                fields.Finish();
                documents.Edit(documents.activeDocumentId, "Delete variable", state => edits.Remove(state, variable.id));
                m_selected = string.Empty;
            }
        }
        finally { ImGui.EndDisabled(); }
    }
}

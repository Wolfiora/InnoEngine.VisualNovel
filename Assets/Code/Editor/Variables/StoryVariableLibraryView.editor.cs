using System;
using System.Linq;
using InnoEngine.Assets;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.Variables;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Editor.Variables;

internal sealed class StoryVariableLibraryView(
    StoryInspectorFields fields,
    ResourceDocumentsModule resources,
    EditorInteractions interactions
)
{
    private bool m_handOver;
    internal bool Draw(
        StoryDocumentsModule documents,
        StoryDraftState draft,
        Action<string> selectNode
    ) {
        ImGui.SeparatorText("Shared variable library");
        var table = draft.ResolveVariableTable();
        if (table is not null)
        {
            EditorUiSession.current.widgets.Hint(table.isMissing ? "Missing variable library · " + draft.variableTablePath : table.title + " · " + table.assetPath);
            if (!table.isMissing && ImGui.Button("Open variable library document")) { fields.Finish(); resources.Open(table.assetPath); }
            if (!table.isMissing && ImGui.Button("Unlink and retain all declarations"))
            { fields.Finish(); documents.Edit(documents.activeDocumentId, "Unlink shared variable library and retain declarations", StoryVariableBindingService.Detach); return true; }
            if (table.isMissing && ImGui.Button("Clear missing reference, keeping graph and chapter declarations"))
            { fields.Finish(); documents.Edit(documents.activeDocumentId, "Clear missing variable library reference", StoryVariableBindingService.ClearMissing); return true; }
            if (!table.isMissing)
            {
                var usages = new Authoring.StoryEditing.VariableEditService(documents.codec);
                foreach (var variable in table.variables)
                {
                    ImGui.Text("Shared · " + variable.name + " · " + variable.id + " · " + variable.initialValue.kind);
                    foreach (string node in usages.FindUsages(draft, variable.id))
                        if (ImGui.Selectable("Locate " + node + "##shared/" + variable.id + "/" + node)) { fields.Finish(); selectNode(node); }
                }
            }
        }
        else EditorUiSession.current.widgets.Hint("Unassigned. Multiple chapters can reference the same Game/Global declarations; call-local variables remain in chapters.");
        fields.Toggle("shared/hand-over", "Allow transferring identical chapter declarations", ref m_handOver);
        if (NovelResourceSelection.Get<VariableTableAsset>(interactions) is { } selected && ImGui.Button("Reference selected shared variable library"))
        { fields.Finish(); documents.Edit(documents.activeDocumentId, "Reference shared variable library", state => StoryVariableBindingService.Attach(state, selected, m_handOver)); return true; }
        if (table is null && ImGui.Button("Create library and transfer Game/Global declarations"))
        {
            fields.Finish();
            var created = resources.Create(new VariableTableAsset { title = draft.title + " · Shared variables", variables = draft.variables.Where(static item => item.scope != VariableScope.Local).Select(VariableValueRules.Copy).ToArray() });
            var canonical = Assets.Load<VariableTableAsset>(AssetPath.Parse(created.assetPath));
            documents.Edit(documents.activeDocumentId, "Create and reference shared variable library", state => StoryVariableBindingService.Attach(state, canonical, true)); return true;
        }
        ImGui.SeparatorText("Chapter declarations");
        return false;
    }
}

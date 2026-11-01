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
        ImGui.SeparatorText(fields.Localize("共享变量库"));
        var table = draft.ResolveVariableTable();
        if (table is not null)
        {
            EditorUiSession.current.widgets.Hint(table.isMissing ? fields.Localize("缺失变量库 · ") + draft.variableTablePath : table.title + " · " + table.assetPath);
            if (!table.isMissing && ImGui.Button(fields.Label("打开变量库文档"))) { fields.Finish(); resources.Open(table.assetPath); }
            if (!table.isMissing && ImGui.Button(fields.Label("解除引用并保留全部声明")))
            { fields.Finish(); documents.Edit(documents.activeDocumentId, fields.Localize("解除共享变量库并保留声明"), StoryVariableBindingService.Detach); return true; }
            if (table.isMissing && ImGui.Button(fields.Label("清除缺失引用，保留图与本章声明")))
            { fields.Finish(); documents.Edit(documents.activeDocumentId, fields.Localize("清除缺失变量库引用"), StoryVariableBindingService.ClearMissing); return true; }
            if (!table.isMissing)
            {
                var usages = new Authoring.StoryEditing.VariableEditService(documents.codec);
                foreach (var variable in table.variables)
                {
                    ImGui.Text(fields.Localize("共享 · ") + variable.name + " · " + variable.id + " · " + variable.initialValue.kind);
                    foreach (string node in usages.FindUsages(draft, variable.id))
                        if (ImGui.Selectable(fields.Caption("定位 " + node + "##shared/" + variable.id + "/" + node, fields.Localize("定位 ") + node + "##shared/" + variable.id + "/" + node))) { fields.Finish(); selectNode(node); }
                }
            }
        }
        else EditorUiSession.current.widgets.Hint(fields.Localize("未分配。可让多个章节引用同一份 Game/Global 声明；调用局部变量保留在章节中。"));
        fields.Toggle("shared/hand-over", "允许移交完全相同的章内声明", ref m_handOver);
        if (NovelResourceSelection.Get<VariableTableAsset>(interactions) is { } selected && ImGui.Button(fields.Label("引用所选共享变量库")))
        { fields.Finish(); documents.Edit(documents.activeDocumentId, fields.Localize("引用共享变量库"), state => StoryVariableBindingService.Attach(state, selected, m_handOver)); return true; }
        if (table is null && ImGui.Button(fields.Label("创建变量库并移交 Game/Global 声明")))
        {
            fields.Finish();
            var created = resources.Create(new VariableTableAsset { title = draft.title + " · 共享变量", variables = draft.variables.Where(static item => item.scope != VariableScope.Local).Select(VariableValueRules.Copy).ToArray() });
            var canonical = Assets.Load<VariableTableAsset>(AssetPath.Parse(created.assetPath));
            documents.Edit(documents.activeDocumentId, fields.Localize("创建并引用共享变量库"), state => StoryVariableBindingService.Attach(state, canonical, true)); return true;
        }
        ImGui.SeparatorText(fields.Localize("章内声明"));
        return false;
    }
}

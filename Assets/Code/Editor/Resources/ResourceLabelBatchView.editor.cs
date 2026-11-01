using System;
using System.Collections.Generic;
using InnoEditor.ImGui;
using Inno.VisualNovel.Authoring.AssetOrganization;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Editor.Inspection;

namespace Inno.VisualNovel.Editor.Resources;

internal sealed class ResourceLabelBatchView(StoryInspectorFields fields)
{
    private bool m_groupOverride, m_descriptionOverride;
    private string m_group = "", m_add = "", m_remove = "", m_description = "";

    internal void Reset() { m_groupOverride = m_descriptionOverride = false; m_group = m_add = m_remove = m_description = ""; }
    internal bool Draw(
        ResourceDocumentsModule documents,
        Guid document,
        IReadOnlyCollection<Guid> selection
    ) {
        if (selection.Count == 0 || !ImGui.CollapsingHeader(fields.Label("批量整理所选资源"))) return false;
        EditorUiSession.current.widgets.Hint(fields.Localize("先移除标签，再添加标签；未启用的分类和说明保留各资源原值。一次应用是一项历史操作。"));
        fields.Toggle("label-batch/group-override", "替换分类（空值表示未分类）", ref m_groupOverride);
        if (m_groupOverride) fields.Text("label-batch/group", "新分类", ref m_group);
        fields.MultilineText("label-batch/add", "添加标签（每行一个）", ref m_add);
        fields.MultilineText("label-batch/remove", "移除标签（每行一个）", ref m_remove);
        fields.Toggle("label-batch/description-override", "替换全部所选说明", ref m_descriptionOverride);
        if (m_descriptionOverride) fields.MultilineText("label-batch/description", "新说明", ref m_description);
        if (!ImGui.Button(fields.Label("应用批量整理"))) return false;
        fields.Finish();
        var change = new ResourceLabelBatchChange { replaceGroup = m_groupOverride, group = m_group, addTags = ParagraphText.Lines(m_add), removeTags = ParagraphText.Lines(m_remove),
            replaceDescription = m_descriptionOverride, description = m_description };
        documents.Edit<ResourceLabelCatalogAsset>(document, fields.Localize("批量整理资源"), candidate => ResourceLabelEditService.ApplyBatch(candidate, selection, change));
        return true;
    }
}

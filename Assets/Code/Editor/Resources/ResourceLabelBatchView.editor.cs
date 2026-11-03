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
        if (selection.Count == 0 || !ImGui.CollapsingHeader("Organize selected resources")) return false;
        EditorUiSession.current.widgets.Hint("Tags are removed before additions. Categories and descriptions retain each resource's value unless explicitly replaced. Apply creates one History operation.");
        fields.Toggle("label-batch/group-override", "Replace category (empty means ungrouped)", ref m_groupOverride);
        if (m_groupOverride) fields.Text("label-batch/group", "New category", ref m_group);
        fields.MultilineText("label-batch/add", "Add tags (one per line)", ref m_add);
        fields.MultilineText("label-batch/remove", "Remove tags (one per line)", ref m_remove);
        fields.Toggle("label-batch/description-override", "Replace descriptions for all selected resources", ref m_descriptionOverride);
        if (m_descriptionOverride) fields.MultilineText("label-batch/description", "New description", ref m_description);
        if (!ImGui.Button("Apply batch annotations")) return false;
        fields.Finish();
        var change = new ResourceLabelBatchChange { replaceGroup = m_groupOverride, group = m_group, addTags = ParagraphText.Lines(m_add), removeTags = ParagraphText.Lines(m_remove),
            replaceDescription = m_descriptionOverride, description = m_description };
        documents.Edit<ResourceLabelCatalogAsset>(document, "Organize resources", candidate => ResourceLabelEditService.ApplyBatch(candidate, selection, change));
        return true;
    }
}

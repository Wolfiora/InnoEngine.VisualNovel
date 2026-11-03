using System;
using System.Linq;
using InnoEditor.ImGui;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Model.Story;

namespace Inno.VisualNovel.Editor.Workspace;

internal sealed class ChapterStructureView(StoryInspectorFields fields)
{
    private readonly StoryStructureService m_edits = new();
    private string m_newEntry = "scene";

    internal void Draw(
        StoryDocumentsModule documents,
        StoryDraftState draft,
        string selected
    ) {
        string title = draft.title;
        if (fields.Text("chapter-title", "Chapter title", ref title))
            documents.Edit(documents.activeDocumentId, "Change chapter title", state => m_edits.SetTitle(state, title), fields.mergeKey);
        ImGui.Text("Chapter ID · " + draft.chapterId);
        ImGui.SeparatorText("Fragments");
        foreach (StoryFragment fragment in draft.fragments)
        {
            string name = fragment.name;
            if (fields.Text(fragment.id + "/name", "Fragment · " + fragment.nodes.Length + " nodes", ref name))
                documents.Edit(documents.activeDocumentId, "Rename fragment", state => m_edits.RenameFragment(state, fragment.id, name), fields.mergeKey);
            if (fragment.nodes.Length == 0 && draft.fragments.Length > 1 && ImGui.Button("Delete empty fragment##" + fragment.id))
            {
                fields.Finish();
                documents.Edit(documents.activeDocumentId, "Delete empty fragment", state => m_edits.RemoveFragment(state, fragment.id));
            }
        }
        if (ImGui.Button("Add fragment"))
        {
            fields.Finish();
            documents.Edit(documents.activeDocumentId, "Add fragment", state => m_edits.AddFragment(state, "New fragment"));
        }
        if (draft.graph.document.FindNode(new(selected.Length == 0 ? "unselected" : selected)) is not null)
        {
            string group = draft.fragments.FirstOrDefault(item => item.nodes.Contains(selected, StringComparer.Ordinal))?.id ?? string.Empty;
            if (fields.Select("move-to-fragment", "Selected node fragment", ref group, draft.fragments.Select(static item => (item.id, item.name))))
                documents.Edit(documents.activeDocumentId, "Move node to fragment", state => m_edits.MoveToFragment(state, selected, group));
        }
        ImGui.SeparatorText("Public entries");
        foreach (StoryEntry entry in draft.entries)
        {
            string target = entry.nodeId;
            if (fields.Select("entry/" + entry.id, entry.id, ref target, draft.graph.document.nodes.Select(static item => (item.id.value, item.id.value))))
                documents.Edit(documents.activeDocumentId, "Change entry target", state => m_edits.SetEntry(state, entry.id, target));
        }
        fields.Text("new-entry-id", "New entry ID", ref m_newEntry);
        ImGui.BeginDisabled(selected.Length == 0);
        try
        {
            if (ImGui.Button("Use selected node as entry"))
            {
                fields.Finish();
                documents.Edit(documents.activeDocumentId, "Register public entry", state => m_edits.SetEntry(state, m_newEntry, selected));
            }
        }
        finally { ImGui.EndDisabled(); }
        EditorUiSession.current.widgets.Hint("Fragment membership and display order do not change story connections. Public entries can be selected for execution and calls.");
    }
}

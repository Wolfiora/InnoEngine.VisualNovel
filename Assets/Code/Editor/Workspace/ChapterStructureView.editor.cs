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
        if (fields.Text("chapter-title", fields.Localize("章节标题"), ref title))
            documents.Edit(documents.activeDocumentId, fields.Localize("修改章节标题"), state => m_edits.SetTitle(state, title), fields.mergeKey);
        ImGui.Text(fields.Localize("章节 ID · ") + draft.chapterId);
        ImGui.SeparatorText(fields.Localize("片段 / Fragments"));
        foreach (StoryFragment fragment in draft.fragments)
        {
            string name = fragment.name;
            if (fields.Text(fragment.id + "/name", fields.Localize("片段 · ") + fragment.nodes.Length + fields.Localize(" 节点"), ref name))
                documents.Edit(documents.activeDocumentId, fields.Localize("修改片段名称"), state => m_edits.RenameFragment(state, fragment.id, name), fields.mergeKey);
            if (fragment.nodes.Length == 0 && draft.fragments.Length > 1 && ImGui.Button(fields.Caption("删除空片段##" + fragment.id, fields.Localize("删除空片段##") + fragment.id)))
            {
                fields.Finish();
                documents.Edit(documents.activeDocumentId, fields.Localize("删除空片段"), state => m_edits.RemoveFragment(state, fragment.id));
            }
        }
        if (ImGui.Button(fields.Label("添加片段 / Add fragment")))
        {
            fields.Finish();
            documents.Edit(documents.activeDocumentId, fields.Localize("添加片段"), state => m_edits.AddFragment(state, "新片段"));
        }
        if (draft.graph.document.FindNode(new(selected.Length == 0 ? "unselected" : selected)) is not null)
        {
            string group = draft.fragments.FirstOrDefault(item => item.nodes.Contains(selected, StringComparer.Ordinal))?.id ?? string.Empty;
            if (fields.Select("move-to-fragment", fields.Localize("所选节点归属"), ref group, draft.fragments.Select(static item => (item.id, item.name))))
                documents.Edit(documents.activeDocumentId, fields.Localize("移动节点到片段"), state => m_edits.MoveToFragment(state, selected, group));
        }
        ImGui.SeparatorText(fields.Localize("公开入口 / Entries"));
        foreach (StoryEntry entry in draft.entries)
        {
            string target = entry.nodeId;
            if (fields.Select("entry/" + entry.id, entry.id, ref target, draft.graph.document.nodes.Select(static item => (item.id.value, item.id.value))))
                documents.Edit(documents.activeDocumentId, fields.Localize("修改入口目标"), state => m_edits.SetEntry(state, entry.id, target));
        }
        fields.Text("new-entry-id", fields.Localize("新入口 ID"), ref m_newEntry);
        ImGui.BeginDisabled(selected.Length == 0);
        try
        {
            if (ImGui.Button(fields.Label("将所选节点设为入口")))
            {
                fields.Finish();
                documents.Edit(documents.activeDocumentId, fields.Localize("注册公开入口"), state => m_edits.SetEntry(state, m_newEntry, selected));
            }
        }
        finally { ImGui.EndDisabled(); }
        EditorUiSession.current.widgets.Hint(fields.Localize("片段归属和显示顺序不改变剧情连接。公开入口可供运行与调用选择。"));
    }
}

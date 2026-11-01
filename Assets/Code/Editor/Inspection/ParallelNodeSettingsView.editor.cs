using System;
using InnoEngine.Graphs;
using InnoEditor.ImGui;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Flow;

namespace Inno.VisualNovel.Editor.Inspection;

internal sealed class ParallelNodeSettingsView(StoryInspectorFields fields)
{
    private string m_remove = string.Empty;
    internal void Draw(
        StoryDocumentsModule documents,
        StoryDraftState draft,
        GraphNodeRecord node
    ) {
        if (node.definitionId == StoryNodeIds.join)
        {
            ImGui.Text(fields.Localize("所属起点：") + documents.codec.Read<JoinNode>(node).forkId);
            EditorUiSession.current.widgets.Hint(fields.Localize("分支在此结束，所属主任务从此节点的 next 继续。汇合点不作为独立章节入口。")); return;
        }
        var parallel = documents.codec.Read<ParallelNode>(node); bool any = parallel.waitAny;
        if (fields.Toggle(node.id + "/any", "任一完成后取消其余任务", ref any))
        { parallel.waitAny = any; documents.Edit(documents.activeDocumentId, fields.Localize("修改并行等待策略"), state => documents.edits.SetSettings(state, node.id, parallel)); }
        ImGui.Text(fields.Localize("汇合点：") + parallel.joinId);
        EditorUiSession.current.widgets.Hint(fields.Localize("分支按列表顺序执行，不创建线程。允许一个分支拥有对白/选项/输入焦点，其他分支继续演出；不同分支可写不同变量，同变量或输入焦点竞争会报错。解锁、页面切换和跨章节调用放在汇合后。取消不撤回已经提交的写入。"));
        var edits = new ParallelEditService(documents.edits, documents.codec);
        if (parallel.branches.Length < 16 && ImGui.Button(fields.Caption("添加并行分支##" + node.id, fields.Localize("添加并行分支##") + node.id)))
        { fields.Finish(); documents.Edit(documents.activeDocumentId, fields.Localize("添加并行分支"), state => edits.AddBranch(state, node.id)); return; }
        foreach (string branch in parallel.branches)
        {
            ImGui.Text(fields.Localize("分支：") + branch);
            int index = Array.IndexOf(parallel.branches, branch);
            if (index > 0 && ImGui.SmallButton(fields.Caption("提前执行##" + node.id + "/" + branch, fields.Localize("提前执行##") + node.id + "/" + branch)))
            { fields.Finish(); documents.Edit(documents.activeDocumentId, fields.Localize("调整并行执行顺序"), state => edits.MoveBranch(state, node.id, branch, index - 1)); return; }
            if (index + 1 < parallel.branches.Length)
            {
                ImGui.SameLine();
                if (ImGui.SmallButton(fields.Caption("延后执行##" + node.id + "/" + branch, fields.Localize("延后执行##") + node.id + "/" + branch)))
                { fields.Finish(); documents.Edit(documents.activeDocumentId, fields.Localize("调整并行执行顺序"), state => edits.MoveBranch(state, node.id, branch, index + 1)); return; }
            }
            if (parallel.branches.Length <= 2) continue;
            if (ImGui.Button(fields.Caption("删除此分支…##" + node.id + "/" + branch, fields.Localize("删除此分支…##") + node.id + "/" + branch))) { fields.Finish(); m_remove = branch; }
            if (m_remove != branch) continue;
            string[] members = edits.Members(draft, node.id, branch);
            EditorUiSession.current.widgets.Hint(fields.Localize("将删除此分支及 ") + members.Length + fields.Localize(" 个内部节点。存在外部引用时操作会拒绝，先重连引用。"));
            if (ImGui.Button(fields.Caption("确认删除分支与内部节点##" + branch, fields.Localize("确认删除分支与内部节点##") + branch)))
            { documents.Edit(documents.activeDocumentId, fields.Localize("删除并行分支"), state => edits.RemoveBranch(state, node.id, branch)); m_remove = string.Empty; return; }
            ImGui.SameLine(); if (ImGui.Button(fields.Caption("取消##" + branch, fields.Localize("取消##") + branch))) m_remove = string.Empty;
        }
    }
}

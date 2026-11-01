using System;
using System.Linq;
using InnoEngine.Graphs;
using InnoEditor.ImGui;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Story.Nodes.Variables;

namespace Inno.VisualNovel.Editor.Inspection;

internal sealed class FlowNodeSettingsView(
    StoryInspectorFields fields,
    InnoEditor.Interactions.EditorInteractions interactions
)
{
    private readonly ExpressionEditorView m_expressions = new(fields);
    private readonly StageNodeSettingsView m_stage = new(fields, interactions);
    private readonly AudioNodeSettingsView m_audio = new(fields, interactions);
    private readonly PerformanceNodeSettingsView m_performance = new(fields, interactions);
    private readonly ImageEffectNodeSettingsView m_images = new(fields, interactions);
    private readonly InputNodeSettingsView m_input = new(fields);
    private readonly CommandNodeSettingsView m_commands = new(fields, interactions);
    private readonly ParallelNodeSettingsView m_parallel = new(fields);
    private readonly SystemNodeSettingsView m_system = new(fields, interactions);
    private string m_remove = string.Empty;

    internal void Draw(
        StoryDocumentsModule documents,
        StoryDraftState draft,
        GraphNodeRecord node
    ) {
        string id = node.id.value;
        switch (node.definitionId)
        {
            case StoryNodeIds.log:
                var log = documents.codec.Read<Model.Story.Nodes.Debugging.LogNode>(node);
                string message = log.message; var level = log.level; bool previewOnly = log.previewOnly;
                bool logChanged = fields.MultilineText(id + "/log-message", fields.Localize("日志（支持 {变量标识}）"), ref message);
                logChanged |= fields.SelectEnum(id + "/log-level", "日志级别", ref level);
                logChanged |= fields.Toggle(id + "/log-preview", "仅在预览输出", ref previewOnly);
                if (logChanged)
                {
                    log.message = message; log.level = level; log.previewOnly = previewOnly;
                    documents.Edit(documents.activeDocumentId, fields.Localize("编辑剧情日志"), candidate => documents.edits.SetSettings(candidate, node.id, log), fields.mergeKey);
                }
                EditorUiSession.current.widgets.Hint(fields.Localize("沿用引擎 Console；消息附带原始章节与节点标识。Error 级别只记录；停止执行请使用断言。回滚重走该指令会再次记录。"));
                break;
            case StoryNodeIds.system: m_system.Draw(documents, node); break;
            case StoryNodeIds.parallel:
            case StoryNodeIds.join: m_parallel.Draw(documents, draft, node); break;
            case StoryNodeIds.command: m_commands.Draw(documents, draft, node); break;
            case StoryNodeIds.unlock:
                UnlockNode unlock = documents.codec.Read<UnlockNode>(node);
                string entryId = unlock.entryId;
                if (fields.Text(id + "/unlock", fields.Localize("收藏条目 ID"), ref entryId))
                {
                    unlock.entryId = entryId;
                    documents.Edit(documents.activeDocumentId, fields.Localize("编辑收藏解锁"), state => documents.edits.SetSettings(state, node.id, unlock), fields.mergeKey);
                }
                EditorUiSession.current.widgets.Hint(fields.Localize("解锁属于全局进度，读档或回滚不会撤销。可在 VN 收藏中查看条目标识。"));
                break;
            case StoryNodeIds.stage: m_stage.Draw(documents, draft, node); break;
            case StoryNodeIds.audio: m_audio.Draw(documents, node); break;
            case StoryNodeIds.performance: m_performance.Draw(documents, node); break;
            case StoryNodeIds.screenEffect: m_images.Screen(documents, node); break;
            case StoryNodeIds.transition: m_images.Transition(documents, node); break;
            case StoryNodeIds.input: m_input.Draw(documents, draft, node); break;
            case StoryNodeIds.wait:
                WaitNode wait = documents.codec.Read<WaitNode>(node);
                double seconds = wait.seconds; bool input = wait.waitForInput;
                bool waitChanged = fields.Toggle(id + "/input", fields.Localize("等待玩家推进"), ref input);
                if (!input) waitChanged |= fields.Number(id + "/seconds", fields.Localize("等待秒数"), ref seconds, 0);
                if (waitChanged)
                {
                    wait.seconds = seconds; wait.waitForInput = input;
                    documents.Edit(documents.activeDocumentId, fields.Localize("编辑等待"), state => documents.edits.SetSettings(state, node.id, wait), fields.mergeKey);
                }
                break;
            case StoryNodeIds.call:
                CallNode call = documents.codec.Read<CallNode>(node);
                string entry = call.entryId;
                string chapter = call.chapterId;
                bool changedCall = fields.Text(id + "/chapter", fields.Localize("目标章节（空为本章）"), ref chapter);
                changedCall |= chapter.Length == 0 ? fields.Select(id + "/entry", fields.Localize("调用入口"), ref entry, draft.entries.Select(static item => (item.id, item.id)))
                    : fields.Text(id + "/entry", fields.Localize("公开入口 ID"), ref entry);
                if (changedCall)
                {
                    call.entryId = entry; call.chapterId = chapter;
                    documents.Edit(documents.activeDocumentId, fields.Localize("编辑片段调用"), state => documents.edits.SetSettings(state, node.id, call));
                }
                if (call.jump) EditorUiSession.current.widgets.Hint(fields.Localize("此指令直接跳转，不增加返回地址。"));
                else if (ImGui.Button(fields.Caption("改为跳转（删除返回连接）##" + id, fields.Localize("改为跳转（删除返回连接）##") + id)))
                {
                    fields.Finish(); call.jump = true;
                    documents.Edit(documents.activeDocumentId, fields.Localize("改为跳转并移除返回连接"), state => documents.edits.SetSettings(state, node.id, call, true));
                }
                if (call.jump && ImGui.Button(fields.Caption("改为调用（需要连接 next）##" + id, fields.Localize("改为调用（需要连接 next）##") + id)))
                {
                    fields.Finish(); call.jump = false;
                    documents.Edit(documents.activeDocumentId, fields.Localize("改为调用"), state => documents.edits.SetSettings(state, node.id, call));
                }
                break;
            case StoryNodeIds.condition:
            case StoryNodeIds.assertion:
                ConditionNode condition = documents.codec.Read<ConditionNode>(node);
                if (m_expressions.Draw(id + "/condition", condition.condition, draft.GetVariables()))
                    documents.Edit(documents.activeDocumentId, fields.Localize("编辑条件"), state => documents.edits.SetSettings(state, node.id, condition), fields.mergeKey);
                break;
            case StoryNodeIds.assign:
                AssignmentNode assignment = documents.codec.Read<AssignmentNode>(node);
                string variable = assignment.variableId;
                bool changed = fields.Select(id + "/variable", fields.Localize("写入变量"), ref variable, draft.GetVariables().Select(static item => (item.id, item.name + " · " + item.id)));
                assignment.variableId = variable;
                changed |= m_expressions.Draw(id + "/value", assignment.value, draft.GetVariables());
                if (changed)
                    documents.Edit(documents.activeDocumentId, fields.Localize("编辑变量赋值"), state => documents.edits.SetSettings(state, node.id, assignment), fields.mergeKey);
                break;
            case StoryNodeIds.random:
                Random(documents, draft, node);
                break;
            case StoryNodeIds.entry:
                EditorUiSession.current.widgets.Hint(fields.Localize("入口按 next 连接进入剧情。可在章节结构中为此节点注册公开入口。"));
                break;
            case StoryNodeIds.end:
                EditorUiSession.current.widgets.Hint(fields.Localize("结束当前故事会话。"));
                break;
            case StoryNodeIds.returnFromCall:
                EditorUiSession.current.widgets.Hint(fields.Localize("返回调用点的 next 连接，并恢复调用方的局部变量。"));
                break;
        }
    }

    private void Random(
        StoryDocumentsModule documents,
        StoryDraftState draft,
        GraphNodeRecord node
    ) {
        RandomNode random = documents.codec.Read<RandomNode>(node);
        bool changed = false;
        string remove = string.Empty;
        foreach (WeightedBranch branch in random.branches)
        {
            string id = node.id + "/" + branch.id;
            double weight = branch.weight;
            changed |= fields.Number(id + "/weight", fields.Localize("权重 ") + branch.id, ref weight, double.Epsilon);
            branch.weight = weight;
            if (ImGui.Button(fields.Caption("删除分支…##" + id, fields.Localize("删除分支…##") + id))) { fields.Finish(); m_remove = id; }
            if (m_remove != id) continue;
            string target = draft.graph.document.edges.FirstOrDefault(edge => edge.output.nodeId == node.id && edge.output.portId.value == branch.id)?.input.nodeId.value ?? "无连接";
            EditorUiSession.current.widgets.Hint(fields.Localize("将移除分支输出 → ") + target);
            if (ImGui.Button(fields.Caption("确认删除分支及其连接##" + id, fields.Localize("确认删除分支及其连接##") + id))) { fields.Finish(); remove = branch.id; m_remove = string.Empty; }
            ImGui.SameLine();
            if (ImGui.Button(fields.Caption("取消##" + id, fields.Localize("取消##") + id))) m_remove = string.Empty;
        }
        if (remove.Length > 0) { random.branches = random.branches.Where(item => item.id != remove).ToArray(); changed = true; }
        if (ImGui.Button(fields.Caption("添加随机分支##" + node.id, fields.Localize("添加随机分支##") + node.id)))
        {
            fields.Finish();
            random.branches = [.. random.branches, new() { id = Guid.NewGuid().ToString("N") }]; changed = true;
        }
        if (changed)
            documents.Edit(documents.activeDocumentId, fields.Localize("编辑随机分支"), state => documents.edits.SetSettings(state, node.id, random, remove.Length > 0), fields.mergeKey);
    }
}

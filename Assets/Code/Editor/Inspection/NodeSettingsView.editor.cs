using System;
using System.Linq;
using System.Collections.Generic;
using InnoEngine.Graphs;
using InnoEditor.ImGui;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Model.Story;

namespace Inno.VisualNovel.Editor.Inspection;

internal sealed class NodeSettingsView(
    StoryInspectorFields fields,
    InnoEditor.Interactions.EditorInteractions interactions,
    Action<IReadOnlyCollection<string>>? selectGroup = null
)
{
    private readonly ReadingNodeSettingsView m_reading = new(fields, interactions);
    private readonly FlowNodeSettingsView m_flow = new(fields, interactions);
    private string m_node = string.Empty;
    private string m_definition = StoryNodeIds.dialogue;
    private readonly StoryPasteView m_paste = new(fields);
    private string m_delete = string.Empty;
    private double m_loopCount = 3;

    internal void Finish() => fields.Finish();
    internal void RequestPaste() { fields.Finish(); m_paste.Request(); }
    internal void CopySelection(
        StoryDocumentsModule documents,
        StoryDraftState draft,
        IReadOnlyCollection<string> nodes
    ) => m_paste.Capture(documents, draft, nodes);

    internal void Draw(
        StoryDocumentsModule documents,
        StoryDraftState draft,
        string selected,
        Action<string> select
    ) {
        if (m_node != selected) { fields.Finish(); m_node = selected; }
        var catalog = new StoryNodeCatalog(documents.codec);
        if (m_paste.Draw(documents, draft, selected) is { } pasted)
        {
            if (pasted.Length > 0) { if (selectGroup is not null) selectGroup(pasted); else select(pasted[0]); }
            return;
        }
        fields.Select("node-creation", fields.Localize("新建指令"), ref m_definition, catalog.definitions.Where(static item => item.id != StoryNodeIds.join).Select(item => (item.id, fields.Localize(item.category) + " / " + fields.Localize(item.displayName))));
        GraphNodeRecord? node = selected.Length > 0 ? draft.graph.document.FindNode(new(selected)) : null;
        if (ImGui.Button(fields.Label("添加指令 / Add instruction")))
        {
            fields.Finish();
            string added = string.Empty;
            documents.Edit(documents.activeDocumentId, fields.Localize("添加剧情指令"), state =>
            {
                string fragment = node is null ? state.fragments.First().id : state.fragments.First(item => item.nodes.Contains(node.id.value, StringComparer.Ordinal)).id;
                GraphNodeId id = new StoryNodeFactory(documents.edits).Create(state, fragment, m_definition,
                    node is null ? new(40, 40) : new(node.position.x + 260, node.position.y));
                added = id.value;
                GraphNodeRecord created = state.graph.document.FindNode(id)!;
                var ports = catalog.TryResolve(created.definitionId, out GraphNodeDefinition? definition) ? definition!.GetPorts(created) : [];
                GraphEdgeRecord? next = node is null ? null : state.graph.document.edges.FirstOrDefault(edge => edge.output.nodeId == node.id && edge.output.portId.value == "next");
                if (next is not null && created.definitionId == StoryNodeIds.parallel)
                {
                    var parallel = documents.codec.Read<Model.Story.Nodes.Flow.ParallelNode>(created);
                    documents.edits.Connect(state, next.output, id); documents.edits.Connect(state, new(new(parallel.joinId), new("next")), next.input.nodeId);
                }
                else if (next is not null && ports.Count(static port => port.direction == GraphPortDirection.Output) == 1 && ports.Any(static port => port.id.value == "next")
                    && ports.Any(static port => port.direction == GraphPortDirection.Input))
                    documents.edits.Insert(state, next.id, id);
            });
            select(added);
            return;
        }
        if (node is null)
        {
            EditorUiSession.current.widgets.Hint(fields.Localize("选择节点或剧本行以编辑参数，也可从上方菜单添加指令。中键平移节点图，滚轮缩放。"));
            return;
        }
        if (ImGui.Button(fields.Label("复制所选节点"))) CopySelection(documents, draft, [node.id.value]);
        GraphEdgeRecord? continuation = draft.graph.document.edges.FirstOrDefault(edge => edge.output.nodeId == node.id && edge.output.portId.value == "next");
        if (continuation is not null)
        {
            fields.Number("counted-loop/iterations", fields.Localize("插入循环的次数"), ref m_loopCount, 0, 1000000);
            if (m_loopCount == System.Math.Floor(m_loopCount) && ImGui.Button(fields.Label("在 next 连接插入次数循环")))
            {
                fields.Finish(); string body = string.Empty;
                documents.Edit(documents.activeDocumentId, fields.Localize("插入可编辑次数循环"), state =>
                {
                    string fragment = state.fragments.Single(item => item.nodes.Contains(node.id.value, StringComparer.Ordinal)).id;
                    StoryLoopTemplate loop = new StoryFlowTemplates(documents.codec).AddCountedLoop(state, fragment, (long)m_loopCount, new(node.position.x + 260, node.position.y + 200));
                    documents.edits.Connect(state, continuation.output, new(loop.entryNode));
                    documents.edits.Connect(state, new(new(loop.exitNode), new("next")), continuation.input.nodeId); body = loop.bodyNode;
                });
                select(body); return;
            }
        }
        ImGui.SeparatorText(node.id.value);
        if (ImGui.CollapsingHeader(fields.Caption("作者备注、标签与书签##" + node.id, fields.Localize("作者备注、标签与书签##") + node.id)))
        {
            var notes = documents.notes.Read(node); string note = notes.note, tags = string.Join("\n", notes.tags); bool bookmarked = notes.bookmarked;
            bool changed = fields.MultilineText(node.id + "/author-note", fields.Localize("作者备注"), ref note);
            changed |= fields.MultilineText(node.id + "/author-tags", fields.Localize("标签（每段一个）"), ref tags);
            changed |= fields.Toggle(node.id + "/bookmark", "书签", ref bookmarked);
            if (changed)
            {
                notes.note = note; notes.tags = tags.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries); notes.bookmarked = bookmarked;
                documents.Edit(documents.activeDocumentId, fields.Localize("编辑作者备注与书签"), state => documents.notes.Set(state, node.id.value, notes), fields.mergeKey);
            }
        }
        if (!catalog.TryResolve(node.definitionId, out GraphNodeDefinition? nodeDefinition))
            EditorUiSession.current.widgets.Hint("Missing · " + node.definitionId + fields.Localize("。原始数据保留，恢复扩展后重新打开。"));
        else
        {
            if (node.definitionId == StoryNodeIds.dialogue) m_reading.Dialogue(documents, node);
            else if (node.definitionId == StoryNodeIds.choice) m_reading.Choice(documents, draft, node);
            else m_flow.Draw(documents, draft, node);
            foreach (GraphPortDefinition port in nodeDefinition!.GetPorts(node).Where(static item => item.direction == GraphPortDirection.Output))
            {
                string target = draft.graph.document.edges.FirstOrDefault(edge => edge.output.nodeId == node.id && edge.output.portId == port.id)?.input.nodeId.value ?? string.Empty;
                if (fields.Select(node.id + "/target/" + port.id.value, fields.Localize("目标 ") + fields.Localize(port.displayName), ref target,
                    new[] { (string.Empty, fields.Localize("未连接")) }.Concat(draft.graph.document.nodes.Where(static item => item.definitionId != StoryNodeIds.entry)
                        .Select(item => (item.id.value, item.id.value)))))
                {
                    string destination = target;
                    documents.Edit(documents.activeDocumentId, fields.Localize("修改执行目标"), state =>
                    {
                        var output = new GraphEndpoint(node.id, port.id);
                        if (destination.Length == 0) documents.edits.Disconnect(state, output);
                        else documents.edits.Connect(state, output, new(destination));
                    });
                }
            }
        }
        bool complex = draft.graph.document.edges.Count(edge => edge.input.nodeId == node.id) > 1 || draft.graph.document.edges.Count(edge => edge.output.nodeId == node.id) > 1;
        if (!draft.entries.Any(entry => entry.nodeId == node.id.value) && ImGui.Button(fields.Caption(complex ? "删除汇合/分支节点…" : "删除线性节点 / Delete linear", complex ? fields.Localize("删除汇合/分支节点…") : fields.Localize("删除线性节点 / Delete linear"))))
        {
            fields.Finish();
            if (complex) m_delete = node.id.value;
            else { documents.Edit(documents.activeDocumentId, fields.Localize("删除剧情节点"), state => documents.edits.Remove(state, node.id)); select(string.Empty); }
        }
        if (m_delete == node.id.value)
        {
            foreach (var edge in draft.graph.document.edges.Where(edge => edge.input.nodeId == node.id || edge.output.nodeId == node.id))
                EditorUiSession.current.widgets.Hint(fields.Localize("将断开：") + edge.output.nodeId + "/" + edge.output.portId + " → " + edge.input.nodeId);
            if (ImGui.Button(fields.Label("确认断开以上连接并删除")))
            { fields.Finish(); documents.Edit(documents.activeDocumentId, fields.Localize("删除复杂节点并显式断开连接"), state => documents.edits.Remove(state, node.id, true)); m_delete = string.Empty; select(string.Empty); }
            ImGui.SameLine(); if (ImGui.Button(fields.Label("取消删除"))) m_delete = string.Empty;
        }
    }
}

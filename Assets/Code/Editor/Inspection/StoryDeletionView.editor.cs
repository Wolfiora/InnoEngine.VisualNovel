using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using InnoEngine.Graphs;
using InnoEditor.ImGui;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Model.Story;

namespace Inno.VisualNovel.Editor.Inspection;

internal sealed class StoryDeletionView(StoryInspectorFields fields)
{
    private StoryDeletionReview? m_review;
    private Guid m_document;
    private long m_revision;
    private readonly Dictionary<GraphEdgeId, string> m_targets = [];
    private readonly HashSet<GraphEdgeId> m_disconnect = [];
    private int m_inputPage, m_edgePage;
    private string m_message = string.Empty;
    internal bool active => m_review is not null;
    internal void Request(
        StoryDocumentsModule documents,
        StoryDraftState draft,
        IReadOnlyCollection<string> nodes
    ) {
        fields.Finish(); var review = documents.deletion.Preview(draft, nodes);
        m_review = review; m_document = documents.activeDocumentId; m_revision = documents.GetRevision(m_document);
        m_targets.Clear(); m_disconnect.Clear(); m_inputPage = m_edgePage = 0; m_message = string.Empty;
        foreach (var input in review.inputs) m_targets.Add(input.id, input.suggestedTarget);
    }
    internal bool Draw(
        StoryDocumentsModule documents,
        StoryDraftState draft
    ) {
        if (m_document != documents.activeDocumentId) { m_review = null; return false; }
        if (m_review is not null && m_revision != documents.GetRevision(m_document))
        { m_review = null; m_message = fields.Localize("文档已经变化，删除审阅已取消；请重新选择并预览。"); }
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
        if (m_review is not { } review) return false;
        bool visible = ImGui.BeginChild("story-delete-review", Vector2.Zero, ImGuiChildFlags.Borders);
        try { return visible && DrawReview(documents, draft, review); }
        finally { ImGui.EndChild(); }
    }
    private bool DrawReview(
        StoryDocumentsModule documents,
        StoryDraftState draft,
        StoryDeletionReview review
    ) {
        ImGui.SeparatorText(fields.Localize("删除审阅 · ") + review.nodes.Count + fields.Localize(" 个节点 / ") + review.connections.Count + fields.Localize(" 条原连接"));
        EditorUiSession.current.widgets.Hint(fields.Localize("单一路径保留可证明的后续；分支、汇合和循环必须明确改接或断开。变量声明和其他资源不会被删除。显式断开可能留下待修复出口，请在诊断通过后保存和运行。"));
        if (ImGui.CollapsingHeader(fields.Label("查看全部受影响连接（分页）")))
        {
            var page = Page("delete-edges", ref m_edgePage, review.connections.Count);
            for (int index = page.first; index < page.end; index++)
            {
                var edge = review.connections[index];
                EditorUiSession.current.widgets.WrappedText(edge.output.nodeId.value + " / " + edge.output.portId.value + " → " + edge.input.nodeId.value);
            }
        }
        var removed = review.nodes.ToHashSet(StringComparer.Ordinal);
        var inputs = Page("delete-inputs", ref m_inputPage, review.inputs.Count);
        for (int index = inputs.first; index < inputs.end; index++)
        {
            var input = review.inputs[index]; string target = m_targets[input.id]; bool disconnect = m_disconnect.Contains(input.id);
            ImGui.SeparatorText(input.output.nodeId.value + " / " + input.output.portId.value);
            if (fields.Toggle("delete/disconnect/" + input.id.value, "明确断开这个来源", ref disconnect))
            { if (disconnect) m_disconnect.Add(input.id); else m_disconnect.Remove(input.id); }
            if (!disconnect && fields.Select("delete/target/" + input.id.value, fields.Localize("替代执行目标"), ref target,
                new[] { (string.Empty, fields.Localize("选择目标（不会自动猜测）")) }.Concat(draft.graph.document.nodes.Where(node => !removed.Contains(node.id.value) && node.definitionId != StoryNodeIds.entry)
                    .Select(static node => (node.id.value, node.id.value))))) m_targets[input.id] = target;
        }
        bool ready = review.inputs.All(input => m_disconnect.Contains(input.id) || m_targets[input.id].Length > 0);
        if (!ready) EditorUiSession.current.widgets.Hint(fields.Localize("尚有 ") + review.inputs.Count(input => !m_disconnect.Contains(input.id) && m_targets[input.id].Length == 0) + fields.Localize(" 个来源需要明确选择；使用分页查看其余来源。"));
        ImGui.BeginDisabled(!ready);
        try
        {
            if (ImGui.Button(fields.Label("应用以上连接并删除所选组")))
            {
                fields.Finish(); var targets = review.inputs.ToDictionary(static input => input.id, input => m_disconnect.Contains(input.id) ? string.Empty : m_targets[input.id]);
                documents.Edit(m_document, fields.Localize("删除已审阅剧情组"), candidate => documents.deletion.Apply(candidate, review, targets));
                m_review = null; return true;
            }
        }
        finally { ImGui.EndDisabled(); }
        ImGui.SameLine(); if (ImGui.Button(fields.Label("取消删除"))) { fields.Finish(); m_review = null; }
        return false;
    }
    private (int first, int end) Page(
        string id,
        ref int page,
        int count
    ) {
        int pages = Math.Max(1, (count + 19) / 20); page = Math.Clamp(page, 0, pages - 1);
        ImGui.BeginDisabled(page == 0);
        try { if (ImGui.SmallButton(fields.Caption("上一页##" + id, fields.Localize("上一页##") + id))) page--; }
        finally { ImGui.EndDisabled(); }
        ImGui.SameLine(); ImGui.Text((page + 1) + " / " + pages + " · " + count + fields.Localize(" 项"));
        ImGui.SameLine(); ImGui.BeginDisabled(page == pages - 1);
        try { if (ImGui.SmallButton(fields.Caption("下一页##" + id, fields.Localize("下一页##") + id))) page++; }
        finally { ImGui.EndDisabled(); }
        return (page * 20, Math.Min(count, (page + 1) * 20));
    }
}

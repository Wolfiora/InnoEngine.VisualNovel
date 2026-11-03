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
        { m_review = null; m_message = "The document changed. Deletion review was cancelled; select the nodes and preview again."; }
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
        ImGui.SeparatorText("Deletion review · " + review.nodes.Count + " nodes / " + review.connections.Count + " original connections");
        EditorUiSession.current.widgets.Hint("A single path retains its unambiguous continuation. Branches, joins and loops require explicit reconnection or disconnection. Variable declarations and other resources are retained. Disconnected outputs may need repair; resolve diagnostics before saving and running.");
        if (ImGui.CollapsingHeader("Review all affected connections (paged)"))
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
            if (fields.Toggle("delete/disconnect/" + input.id.value, "Explicitly disconnect this source", ref disconnect))
            { if (disconnect) m_disconnect.Add(input.id); else m_disconnect.Remove(input.id); }
            if (!disconnect && fields.Select("delete/target/" + input.id.value, "Replacement execution target", ref target,
                new[] { (string.Empty, "Choose a target (no automatic inference)") }.Concat(draft.graph.document.nodes.Where(node => !removed.Contains(node.id.value) && node.definitionId != StoryNodeIds.entry)
                    .Select(static node => (node.id.value, node.id.value))))) m_targets[input.id] = target;
        }
        bool ready = review.inputs.All(input => m_disconnect.Contains(input.id) || m_targets[input.id].Length > 0);
        if (!ready) EditorUiSession.current.widgets.Hint("Remaining: " + review.inputs.Count(input => !m_disconnect.Contains(input.id) && m_targets[input.id].Length == 0) + " sources need an explicit choice. Use the pages to review the remaining sources.");
        ImGui.BeginDisabled(!ready);
        try
        {
            if (ImGui.Button("Apply these connections and delete the selected group"))
            {
                fields.Finish(); var targets = review.inputs.ToDictionary(static input => input.id, input => m_disconnect.Contains(input.id) ? string.Empty : m_targets[input.id]);
                documents.Edit(m_document, "Delete reviewed story group", candidate => documents.deletion.Apply(candidate, review, targets));
                m_review = null; return true;
            }
        }
        finally { ImGui.EndDisabled(); }
        ImGui.SameLine(); if (ImGui.Button("Cancel deletion")) { fields.Finish(); m_review = null; }
        return false;
    }
    private (int first, int end) Page(
        string id,
        ref int page,
        int count
    ) {
        int pages = Math.Max(1, (count + 19) / 20); page = Math.Clamp(page, 0, pages - 1);
        ImGui.BeginDisabled(page == 0);
        try { if (ImGui.SmallButton("Previous page##" + id)) page--; }
        finally { ImGui.EndDisabled(); }
        ImGui.SameLine(); ImGui.Text((page + 1) + " / " + pages + " · " + count + " items");
        ImGui.SameLine(); ImGui.BeginDisabled(page == pages - 1);
        try { if (ImGui.SmallButton("Next page##" + id)) page++; }
        finally { ImGui.EndDisabled(); }
        return (page * 20, Math.Min(count, (page + 1) * 20));
    }
}

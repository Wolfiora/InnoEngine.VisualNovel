using System;
using System.Collections.Generic;
using System.Linq;
using InnoEditor.ImGui;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Model.Story;

namespace Inno.VisualNovel.Editor.Inspection;

internal sealed class StoryPasteView(StoryInspectorFields fields)
{
    private byte[]? m_bytes;
    private StoryClipboardReview? m_review;
    private readonly Dictionary<string, string> m_targets = new(StringComparer.Ordinal);
    private Guid m_document;
    private string m_fragment = string.Empty;
    private bool m_open;
    private bool m_requested;
    private readonly StoryPasteVariablesView m_variables = new(fields);
    internal void Request() => m_requested = true;

    internal void Capture(
        StoryDocumentsModule documents,
        StoryDraftState draft,
        IReadOnlyCollection<string> nodes
    ) {
        fields.Finish();
        byte[] bytes = documents.clipboard.Capture(draft, nodes); var review = documents.clipboard.Review(bytes);
        m_bytes = bytes; m_review = review; m_document = Guid.Empty; m_targets.Clear(); m_fragment = string.Empty; m_open = false;
        m_variables.Capture(review); m_requested = false;
    }
    internal string[]? Draw(
        StoryDocumentsModule documents,
        StoryDraftState draft,
        string selected
    ) {
        if (m_bytes is null || m_review is null) { m_requested = false; return null; }
        if (m_document != documents.activeDocumentId)
        {
            m_document = documents.activeDocumentId; m_targets.Clear(); m_open = false;
            m_variables.Reset();
            m_fragment = draft.fragments.FirstOrDefault(fragment => fragment.nodes.Contains(selected, StringComparer.Ordinal))?.id ?? draft.fragments.First().id;
            foreach (string target in m_review.externalTargets)
                m_targets.Add(target, m_review.chapterId == draft.chapterId && draft.graph.document.FindNode(new(target)) is not null ? target : string.Empty);
        }
        if (!draft.fragments.Any(fragment => fragment.id == m_fragment)) m_fragment = draft.fragments.First().id;
        var declarations = draft.GetVariables().ToDictionary(static variable => variable.id, StringComparer.Ordinal);
        bool mandatory = m_review.chapterId != draft.chapterId && m_review.externalTargets.Count > 0 || m_variables.Conflicts(declarations);
        bool requested = m_requested; m_requested = false;
        if (ImGui.Button("Paste copied " + m_review.nodeIds.Count + " items") || requested)
        {
            fields.Finish();
            if (mandatory) m_open = true;
            else return Apply();
        }
        ImGui.SameLine(); if (ImGui.SmallButton(m_open ? "Close paste review" : "Paste location and targets…")) { fields.Finish(); m_open = !m_open; }
        if (!m_open) return null;
        fields.Select("paste/fragment", "Destination fragment", ref m_fragment, draft.fragments.Select(static fragment => (fragment.id, fragment.name)));
        EditorUiSession.current.widgets.Hint("Internal nodes, dialogue lines, choices and edges receive new identities. Only the external targets below are mapped. Unresolved external targets remain explicitly Missing; use diagnostics and connection fields to resolve them after pasting.");
        foreach (string target in m_review.externalTargets)
        {
            string mapped = m_targets[target];
            if (fields.Select("paste/target/" + target, "External target · " + target, ref mapped,
                new[] { (string.Empty, "Keep as Missing (do not infer a target)") }.Concat(draft.graph.document.nodes.Where(static node => node.definitionId != StoryNodeIds.entry)
                    .Select(static node => (node.id.value, node.id.value))))) m_targets[target] = mapped;
        }
        m_variables.Draw(declarations);
        if (!ImGui.Button("Paste with these locations and targets")) return null;
        fields.Finish(); return Apply();

        string[] Apply()
        {
            string[] created = []; var targets = m_targets.Where(static pair => pair.Value.Length > 0).ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
            var variables = m_variables.Mappings();
            documents.Edit(documents.activeDocumentId, "Paste reviewed story group", candidate => created = documents.clipboard.Paste(candidate, m_fragment, m_bytes, new(100, 100), targets, variables));
            m_open = false; return created;
        }
    }
}

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
        fields.Select("node-creation", "New instruction", ref m_definition, catalog.definitions.Where(static item => item.id != StoryNodeIds.join).Select(item => (item.id, item.category + " / " + item.displayName)));
        GraphNodeRecord? node = selected.Length > 0 ? draft.graph.document.FindNode(new(selected)) : null;
        if (ImGui.Button("Add instruction"))
        {
            fields.Finish();
            string added = string.Empty;
            documents.Edit(documents.activeDocumentId, "Add story instruction", state =>
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
            EditorUiSession.current.widgets.Hint("Select a node or script row to edit its parameters, or add an instruction from the menu above. Pan the graph with the middle mouse button and zoom with the wheel.");
            return;
        }
        if (ImGui.Button("Copy selected nodes")) CopySelection(documents, draft, [node.id.value]);
        GraphEdgeRecord? continuation = draft.graph.document.edges.FirstOrDefault(edge => edge.output.nodeId == node.id && edge.output.portId.value == "next");
        if (continuation is not null)
        {
            fields.Number("counted-loop/iterations", "Iterations for inserted loop", ref m_loopCount, 0, 1000000);
            if (m_loopCount == System.Math.Floor(m_loopCount) && ImGui.Button("Insert counted loop on the next connection"))
            {
                fields.Finish(); string body = string.Empty;
                documents.Edit(documents.activeDocumentId, "Insert editable counted loop", state =>
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
        if (ImGui.CollapsingHeader("Author notes, tags and bookmarks##" + node.id))
        {
            var notes = documents.notes.Read(node); string note = notes.note, tags = string.Join("\n", notes.tags); bool bookmarked = notes.bookmarked;
            bool changed = fields.MultilineText(node.id + "/author-note", "Author notes", ref note);
            changed |= fields.MultilineText(node.id + "/author-tags", "Tags (one per paragraph)", ref tags);
            changed |= fields.Toggle(node.id + "/bookmark", "Bookmark", ref bookmarked);
            if (changed)
            {
                notes.note = note; notes.tags = tags.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries); notes.bookmarked = bookmarked;
                documents.Edit(documents.activeDocumentId, "Edit author notes and bookmark", state => documents.notes.Set(state, node.id.value, notes), fields.mergeKey);
            }
        }
        if (!catalog.TryResolve(node.definitionId, out GraphNodeDefinition? nodeDefinition))
            EditorUiSession.current.widgets.Hint("Missing · " + node.definitionId + ". Original data is retained. Restore the extension and reopen this document.");
        else
        {
            if (node.definitionId == StoryNodeIds.dialogue) m_reading.Dialogue(documents, node);
            else if (node.definitionId == StoryNodeIds.choice) m_reading.Choice(documents, draft, node);
            else m_flow.Draw(documents, draft, node);
            foreach (GraphPortDefinition port in nodeDefinition!.GetPorts(node).Where(static item => item.direction == GraphPortDirection.Output))
            {
                string target = draft.graph.document.edges.FirstOrDefault(edge => edge.output.nodeId == node.id && edge.output.portId == port.id)?.input.nodeId.value ?? string.Empty;
                if (fields.Select(node.id + "/target/" + port.id.value, "Target " + port.displayName, ref target,
                    new[] { (string.Empty, "Disconnected") }.Concat(draft.graph.document.nodes.Where(static item => item.definitionId != StoryNodeIds.entry)
                        .Select(item => (item.id.value, item.id.value)))))
                {
                    string destination = target;
                    documents.Edit(documents.activeDocumentId, "Change execution target", state =>
                    {
                        var output = new GraphEndpoint(node.id, port.id);
                        if (destination.Length == 0) documents.edits.Disconnect(state, output);
                        else documents.edits.Connect(state, output, new(destination));
                    });
                }
            }
        }
        bool complex = draft.graph.document.edges.Count(edge => edge.input.nodeId == node.id) > 1 || draft.graph.document.edges.Count(edge => edge.output.nodeId == node.id) > 1;
        if (!draft.entries.Any(entry => entry.nodeId == node.id.value) && ImGui.Button(complex ? "Delete join / branch node…" : "Delete linear node"))
        {
            fields.Finish();
            if (complex) m_delete = node.id.value;
            else { documents.Edit(documents.activeDocumentId, "Delete story node", state => documents.edits.Remove(state, node.id)); select(string.Empty); }
        }
        if (m_delete == node.id.value)
        {
            foreach (var edge in draft.graph.document.edges.Where(edge => edge.input.nodeId == node.id || edge.output.nodeId == node.id))
                EditorUiSession.current.widgets.Hint("Connections to disconnect: " + edge.output.nodeId + "/" + edge.output.portId + " → " + edge.input.nodeId);
            if (ImGui.Button("Disconnect these connections and delete"))
            { fields.Finish(); documents.Edit(documents.activeDocumentId, "Delete complex node and explicitly disconnect its edges", state => documents.edits.Remove(state, node.id, true)); m_delete = string.Empty; select(string.Empty); }
            ImGui.SameLine(); if (ImGui.Button("Cancel deletion")) m_delete = string.Empty;
        }
    }
}

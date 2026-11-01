using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Graphs;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Authoring.Diagnostics;

namespace Inno.VisualNovel.Authoring.StoryEditing;

internal sealed class StoryConnectionIndex
{
    private readonly Dictionary<GraphNodeId, GraphNodeRecord> m_nodes;
    private readonly Dictionary<GraphNodeId, GraphEdgeRecord[]> m_incoming, m_outgoing;
    private readonly Dictionary<GraphNodeId, string[]> m_fragments;
    private readonly HashSet<GraphNodeId> m_entries;
    private readonly StoryNodeCatalog m_catalog;
    internal StoryConnectionIndex(
        StoryDraftState draft,
        StoryNodeCatalog catalog
    ) {
        m_nodes = draft.graph.document.nodes.ToDictionary(static node => node.id);
        m_incoming = draft.graph.document.edges.GroupBy(static edge => edge.input.nodeId).ToDictionary(static group => group.Key, static group => group.ToArray());
        m_outgoing = draft.graph.document.edges.GroupBy(static edge => edge.output.nodeId).ToDictionary(static group => group.Key, static group => group.ToArray());
        m_fragments = draft.fragments.SelectMany(fragment => fragment.nodes.Select(node => (node: new GraphNodeId(node), fragment: fragment.id)))
            .GroupBy(static item => item.node).ToDictionary(static group => group.Key, static group => group.Select(static item => item.fragment).ToArray());
        m_entries = draft.entries.Select(static entry => new GraphNodeId(entry.nodeId)).ToHashSet(); m_catalog = catalog;
    }
    internal bool Linear(GraphNodeId id)
    {
        if (!m_nodes.TryGetValue(id, out var node) || m_entries.Contains(id) || node.definitionId is StoryNodeIds.entry or StoryNodeIds.parallel or StoryNodeIds.join
            || !m_incoming.TryGetValue(id, out var incoming) || incoming.Length != 1 || !m_outgoing.TryGetValue(id, out var outgoing) || outgoing.Length != 1
            || !m_catalog.TryResolve(node.definitionId, out var definition)) return false;
        var outputs = definition!.GetPorts(node).Where(static port => port.direction == GraphPortDirection.Output).ToArray();
        return outputs.Length == 1 && outputs[0].id.value == "next";
    }
    internal GraphEdgeRecord Before(GraphNodeId id) => m_incoming[id].Single();
    internal GraphEdgeRecord After(GraphNodeId id) => m_outgoing[id].Single();
    internal string Fragment(GraphNodeId id) => m_fragments.TryGetValue(id, out var owners) && owners.Length == 1 ? owners[0]
        : throw new AuthoringValidationException($"节点需要唯一片段归属。");
}

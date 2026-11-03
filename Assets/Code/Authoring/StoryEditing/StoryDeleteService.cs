using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Graphs;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Authoring.Diagnostics;

namespace Inno.VisualNovel.Authoring.StoryEditing;

/// <summary>Reviews exact group deletion boundaries and validates every replacement before mutating a candidate.</summary>
public sealed class StoryDeleteService(
    StoryEditService edits,
    StoryNodeDataCodec codec
)
{
    private readonly StoryNodeCatalog m_catalog = new(codec);
    /// <summary>Expands selected parallel fork/join boundaries to their complete bounded groups, preserving ordinary selection.</summary>
    /// <param name="draft">The current chapter.</param>
    /// <param name="nodes">The explicitly selected existing identities.</param>
    /// <returns>Stable authored membership including nested scopes needed by selected boundaries.</returns>
    public IReadOnlyList<string> ExpandBoundaries(
        StoryDraftState draft,
        IReadOnlyCollection<string> nodes
    ) {
        var result = nodes.ToHashSet(StringComparer.Ordinal); var scopes = new ParallelEditService(edits, codec);
        if (result.Any(id => draft.graph.document.FindNode(new(id)) is null)) throw new AuthoringValidationException($"The selection contains nodes that no longer exist.");
        if (!draft.graph.document.nodes.Any(node => result.Contains(node.id.value) && node.definitionId is StoryNodeIds.parallel or StoryNodeIds.join))
            return Array.AsReadOnly(draft.graph.document.nodes.Where(node => result.Contains(node.id.value)).Select(static node => node.id.value).ToArray());
        foreach (var fork in draft.graph.document.nodes.Where(static node => node.definitionId == StoryNodeIds.parallel))
        {
            var definition = codec.Read<ParallelNode>(fork);
            if (result.Contains(fork.id.value) || result.Contains(definition.joinId)) result.UnionWith(Group(fork, definition, scopes));
        }
        return Array.AsReadOnly(draft.graph.document.nodes.Where(node => result.Contains(node.id.value)).Select(static node => node.id.value).ToArray());

        IEnumerable<string> Group(
            GraphNodeRecord fork,
            ParallelNode definition,
            ParallelEditService scope
        )
            => new[] { fork.id.value, definition.joinId }.Concat(definition.branches.SelectMany(branch => scope.Members(draft, fork.id, branch)));
    }
    /// <summary>Captures affected edges and safe ordinary linear suggestions without changing graph or layout.</summary>
    /// <param name="draft">The current shared chapter.</param>
    /// <param name="nodes">The exact group; public entries and incomplete parallel boundaries are rejected.</param>
    /// <returns>The immutable review used by every authoring view.</returns>
    public StoryDeletionReview Preview(
        StoryDraftState draft,
        IReadOnlyCollection<string> nodes
    ) {
        var graph = draft.graph.document; var indexed = graph.nodes.ToDictionary(static node => node.id.value, StringComparer.Ordinal);
        var ids = nodes.ToHashSet(StringComparer.Ordinal);
        if (ids.Count is < 1 or > 10000 || ids.Any(id => !indexed.ContainsKey(id))) throw new AuthoringValidationException($"The deletion group contains missing nodes or exceeds the count limit.");
        var entries = draft.entries.Where(entry => ids.Contains(entry.nodeId)).ToArray();
        if (entries.Length > 0) throw new AuthoringValidationException($"Reassign these public entries before deleting their nodes: {string.Join(", ", entries.Select(static entry => entry.id))}");
        if (!ExpandBoundaries(draft, ids).All(ids.Contains)) throw new AuthoringValidationException($"A selected parallel fork or join must be deleted with its complete group. Include the missing scope boundaries first.");
        var incoming = graph.edges.GroupBy(static edge => edge.input.nodeId.value).ToDictionary(static group => group.Key, static group => group.ToArray(), StringComparer.Ordinal);
        var outgoing = graph.edges.GroupBy(static edge => edge.output.nodeId.value).ToDictionary(static group => group.Key, static group => group.ToArray(), StringComparer.Ordinal);
        var affected = graph.edges.Where(edge => ids.Contains(edge.input.nodeId.value) || ids.Contains(edge.output.nodeId.value)).ToArray();
        return new(draft.chapterId, graph.nodes.Where(node => ids.Contains(node.id.value)).Select(static node => node.id.value).ToArray(),
            affected.Select(static edge => new StoryDeletionLink(edge.id, edge.output, edge.input)).ToArray(),
            affected.Where(edge => !ids.Contains(edge.output.nodeId.value) && ids.Contains(edge.input.nodeId.value))
                .Select(edge => new StoryDeletionInput(edge.id, edge.output, edge.input.nodeId.value, Suggest(edge.input.nodeId.value))).ToArray());

        string Suggest(string current)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (ids.Contains(current))
            {
                if (!visited.Add(current) || incoming.GetValueOrDefault(current)?.Length != 1 || outgoing.GetValueOrDefault(current)?.Length != 1
                    || indexed[current].definitionId is StoryNodeIds.parallel or StoryNodeIds.join || !m_catalog.TryResolve(indexed[current].definitionId, out var definition)) return string.Empty;
                try
                {
                    var outputs = definition!.GetPorts(indexed[current]).Where(static port => port.direction == GraphPortDirection.Output).ToArray();
                    if (outputs.Length != 1 || outputs[0].id.value != "next") return string.Empty;
                }
                catch (Exception error) when (error is not OutOfMemoryException) { return string.Empty; }
                current = outgoing[current][0].input.nodeId.value;
            }
            return indexed.ContainsKey(current) ? current : string.Empty;
        }
    }
    /// <summary>Applies an unchanged proposal after checking every outside source; empty mapped targets explicitly disconnect.</summary>
    /// <param name="draft">The caller's detached document candidate.</param>
    /// <param name="review">The previously displayed group and connection snapshot.</param>
    /// <param name="targets">Optional decisions by incoming edge; ambiguous sources require an explicit target or empty disconnection.</param>
    public void Apply(
        StoryDraftState draft,
        StoryDeletionReview review,
        IReadOnlyDictionary<GraphEdgeId, string>? targets = null
    ) {
        if (review.chapterId != draft.chapterId) throw new AuthoringValidationException($"The deletion review belongs to another chapter.");
        var current = Preview(draft, review.nodes);
        if (!current.nodes.SequenceEqual(review.nodes) || !current.connections.SequenceEqual(review.connections) || !current.inputs.SequenceEqual(review.inputs))
            throw new AuthoringValidationException($"The deletion boundary changed. Review its connections again.");
        var allowed = current.inputs.Select(static input => input.id).ToHashSet();
        if (targets is not null && targets.Keys.Any(id => !allowed.Contains(id))) throw new AuthoringValidationException($"The reconnection decisions contain a source outside this deletion group.");
        var removed = current.nodes.ToHashSet(StringComparer.Ordinal); var additions = new List<GraphEdgeRecord>();
        var occupied = draft.graph.document.edges.Where(edge => !removed.Contains(edge.output.nodeId.value) && !removed.Contains(edge.input.nodeId.value)).Select(static edge => edge.output).ToHashSet();
        foreach (var input in current.inputs)
        {
            string? target = targets?.TryGetValue(input.id, out string? explicitTarget) == true ? explicitTarget : input.suggestedTarget.Length > 0 ? input.suggestedTarget : null;
            if (target is null) throw new AuthoringValidationException($"Explicitly select a replacement target or disconnect this source: {input.output.nodeId.value}/{input.output.portId.value}");
            if (target.Length == 0) continue;
            var node = draft.graph.document.FindNode(new(target));
            if (removed.Contains(target) || node is null || !m_catalog.TryResolve(node.definitionId, out var definition)
                || !definition!.GetPorts(node).Any(static port => port.direction == GraphPortDirection.Input && port.id.value == "in") || !occupied.Add(input.output))
                throw new AuthoringValidationException($"The replacement target or execution output is invalid: {target}");
            additions.Add(new(input.id, input.output, new(new(target), new("in"))));
        }
        foreach (string id in current.nodes) draft.graph.document.RemoveNode(new(id));
        foreach (var fragment in draft.fragments) fragment.nodes = fragment.nodes.Where(id => !removed.Contains(id)).ToArray();
        foreach (var edge in additions) draft.graph.document.AddEdge(edge);
    }
}

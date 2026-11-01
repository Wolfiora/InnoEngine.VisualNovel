using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Graphs;
using Inno.VisualNovel.Authoring.Documents;

namespace Inno.VisualNovel.Authoring.ScriptProjection;

/// <summary>Describes a script-visible row and its explicit non-linear links.</summary>
/// <param name="nodeId">The existing source node identity.</param>
/// <param name="fragmentId">The author-assigned fragment identity.</param>
/// <param name="incoming">The number of incoming execution paths.</param>
/// <param name="targets">Explicit outgoing edges; cycles are never recursively expanded.</param>
public sealed record ScriptRow(
    string nodeId,
    string fragmentId,
    int incoming,
    IReadOnlyList<GraphEdgeRecord> targets
)
{
    /// <summary>Gets whether this row begins a basic execution segment, including cycle and public-entry boundaries.</summary>
    public bool startsBlock { get; init; }
}

/// <summary>Projects a free graph to finite script rows without rewriting or duplicating semantics.</summary>
public static class ScriptProjectionBuilder
{
    /// <summary>Builds every node exactly once, including disconnected and Missing nodes.</summary>
    /// <param name="draft">The shared authored state.</param>
    /// <returns>A finite ordered script projection with original stable identities.</returns>
    public static IReadOnlyList<ScriptRow> Build(StoryDraftState draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        GraphDocument graph = draft.graph.document;
        var nodes = graph.nodes.ToDictionary(static node => node.id.value, StringComparer.Ordinal);
        var incoming = graph.edges.GroupBy(static edge => edge.input.nodeId.value).ToDictionary(static group => group.Key, static group => group.ToArray(), StringComparer.Ordinal);
        var outgoing = graph.edges.GroupBy(static edge => edge.output.nodeId.value).ToDictionary(static group => group.Key, static group => group.ToArray(), StringComparer.Ordinal);
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var fragment in draft.fragments) foreach (string id in fragment.nodes) if (nodes.ContainsKey(id)) owners.TryAdd(id, fragment.id);
        var entries = draft.entries.Select(static entry => entry.nodeId).Where(nodes.ContainsKey).ToHashSet(StringComparer.Ordinal);
        var entryGroups = draft.entries.Select(static entry => entry.nodeId).Where(nodes.ContainsKey).Distinct(StringComparer.Ordinal)
            .GroupBy(id => owners.GetValueOrDefault(id, string.Empty)).ToDictionary(static group => group.Key, static group => group.ToArray(), StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<ScriptRow>();
        foreach (var fragment in draft.fragments)
            Group(fragment.nodes.Where(id => nodes.ContainsKey(id) && owners.GetValueOrDefault(id, string.Empty) == fragment.id).Distinct(StringComparer.Ordinal).ToArray(), fragment.id);
        Group(graph.nodes.Select(static node => node.id.value).Where(id => !owners.ContainsKey(id)).ToArray(), string.Empty);
        return rows;

        void Group(
            string[] members,
            string fragment
        ) {
            foreach (string id in entryGroups.GetValueOrDefault(fragment) ?? []) Block(id, fragment);
            foreach (string id in members)
            {
                var previous = incoming.GetValueOrDefault(id) ?? [];
                if (previous.Length != 1 || outgoing.GetValueOrDefault(previous[0].output.nodeId.value)?.Length != 1
                    || owners.GetValueOrDefault(previous[0].output.nodeId.value, string.Empty) != fragment) Block(id, fragment);
            }
            // Pure cycles have no natural start; authored membership supplies a finite stable anchor.
            foreach (string id in members) Block(id, fragment);
        }
        void Block(
            string id,
            string fragment
        ) {
            bool first = true;
            while (nodes.ContainsKey(id) && owners.GetValueOrDefault(id, string.Empty) == fragment && seen.Add(id))
            {
                var targets = outgoing.GetValueOrDefault(id) ?? [];
                rows.Add(new(id, fragment, incoming.GetValueOrDefault(id)?.Length ?? 0, targets) { startsBlock = first }); first = false;
                if (targets.Length != 1) return;
                string next = targets[0].input.nodeId.value;
                if (entries.Contains(next) || incoming.GetValueOrDefault(next)?.Length != 1) return;
                id = next;
            }
        }
    }
}

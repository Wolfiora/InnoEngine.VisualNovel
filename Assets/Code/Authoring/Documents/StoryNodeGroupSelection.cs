using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Graphs;
using InnoEditor.Graph;

namespace Inno.VisualNovel.Authoring.Documents;

/// <summary>Shares native graph selection membership between graph and script views without owning a second selection store.</summary>
public static class StoryNodeGroupSelection
{
    /// <summary>Applies a plain, additive or range click to the native selection in stable script projection order.</summary>
    /// <param name="selection">The workspace-owned native graph state.</param>
    /// <param name="order">Unique current projection identities, excluding no valid node kinds.</param>
    /// <param name="nodeId">The clicked node.</param>
    /// <param name="anchor">The previous ordinary click used for a range.</param>
    /// <param name="range">Whether the author requested a contiguous visible-order range.</param>
    /// <param name="additive">Whether existing membership is retained, or the clicked member is toggled.</param>
    /// <returns>The new primary identity, empty when the final selected node was removed.</returns>
    public static string Choose(
        GraphCanvasState selection,
        IReadOnlyList<string> order,
        string nodeId,
        string anchor,
        bool range,
        bool additive
    ) {
        ArgumentNullException.ThrowIfNull(selection); ArgumentNullException.ThrowIfNull(order);
        var ids = order.Select(static id => new GraphNodeId(id)).ToArray();
        var known = ids.ToHashSet(); var clicked = new GraphNodeId(nodeId);
        if (known.Count != ids.Length || !known.Contains(clicked)) throw new ArgumentException("Selection requires a unique current projection and an existing clicked identity.");
        var members = selection.selectedNodes.Where(known.Contains).ToHashSet();
        int start = Array.FindIndex(ids, id => id.value == anchor), end = Array.IndexOf(ids, clicked);
        if (range && start >= 0)
        {
            if (!additive) members.Clear();
            for (int index = Math.Min(start, end); index <= Math.Max(start, end); index++) members.Add(ids[index]);
        }
        else if (additive) { if (!members.Remove(clicked)) members.Add(clicked); }
        else { members.Clear(); members.Add(clicked); }
        selection.SelectNodes(ids.Where(members.Contains));
        return members.Contains(clicked) ? nodeId : ids.LastOrDefault(members.Contains).value ?? string.Empty;
    }
    /// <summary>Prunes deleted members and adopts an explicitly selected primary through the same native state.</summary>
    /// <param name="selection">The shared native membership.</param>
    /// <param name="graph">The current authoritative document.</param>
    /// <param name="primary">The requested primary node, possibly deleted.</param>
    /// <returns>A live primary, or empty when the document has no retained selection.</returns>
    public static string Reconcile(
        GraphCanvasState selection,
        GraphDocument graph,
        string primary
    ) {
        var known = graph.nodes.Select(static node => node.id).ToHashSet();
        var live = selection.selectedNodes.Where(known.Contains).ToArray();
        if (primary.Length > 0 && known.Contains(new(primary)))
        {
            if (!live.Any(id => id.value == primary)) live = [new(primary)];
        }
        else primary = live.LastOrDefault().value ?? string.Empty;
        if (!selection.selectedNodes.SequenceEqual(live)) selection.SelectNodes(live);
        return primary;
    }
}

using System;
using System.Linq;
using InnoEngine.Graphs;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Authoring.Diagnostics;

namespace Inno.VisualNovel.Authoring.StoryEditing;

/// <summary>Creates ordinary instructions and complete beginner branching templates over the authoritative graph.</summary>
public sealed class StoryQuickInsertService(
    StoryEditService edits,
    StoryNodeDataCodec codec
)
{
    /// <summary>Finds the explicitly selected instruction's unique next continuation without guessing a branch target.</summary>
    /// <param name="draft">The current shared story draft.</param>
    /// <param name="selected">The primary selected node, or empty.</param>
    /// <returns>The exact next edge, or null when the author must connect the created instruction explicitly.</returns>
    public GraphEdgeId? NextEdge(
        StoryDraftState draft,
        string selected
    ) {
        var edges = draft.graph.document.edges.Where(edge => edge.output.nodeId.value == selected && edge.output.portId.value == "next").ToArray();
        return edges.Length == 1 ? edges[0].id : null;
    }
    /// <summary>Creates a node in a detached document transaction, preserving existing paths when a definition cannot be inserted.</summary>
    /// <param name="draft">The detached candidate owned by the native document edit transaction.</param>
    /// <param name="fragmentId">The explicitly chosen owning fragment.</param>
    /// <param name="definitionId">A built-in instruction definition, excluding public entries and standalone joins.</param>
    /// <param name="position">The initial graph layout position.</param>
    /// <param name="edgeId">An optional exact next edge; null creates an unconnected instruction.</param>
    /// <returns>The selected entry and connection status; failed candidates must be discarded by the document transaction.</returns>
    public StoryInsertionResult Create(
        StoryDraftState draft,
        string fragmentId,
        string definitionId,
        GraphPosition position,
        GraphEdgeId? edgeId = null
    ) {
        if (definitionId is StoryNodeIds.entry or StoryNodeIds.join) throw new ArgumentException("Entries and owned joins are created through chapter or parallel tools.", nameof(definitionId));
        _ = draft.fragments.Single(fragment => fragment.id == fragmentId);
        var edge = edgeId is { } selected ? draft.graph.document.edges.Single(item => item.id == selected) : null;
        if (edge is not null && edge.output.portId.value != "next") throw new AuthoringValidationException($"Quick insertion requires an explicit next edge. Edit branch outputs in the connection editor.");
        if (definitionId == StoryNodeIds.dialogue && edge is not null
            && draft.graph.document.FindNode(edge.output.nodeId)?.definitionId == StoryNodeIds.dialogue
            && draft.fragments.Single(fragment => fragment.nodes.Contains(edge.output.nodeId.value, StringComparer.Ordinal)).id == fragmentId)
        {
            GraphNodeId continued = edits.ContinueDialogue(draft, edge.output.nodeId);
            draft.graph.document.FindNode(continued)!.position = position; return new(continued, true);
        }
        GraphNodeId created = new StoryNodeFactory(edits).Create(draft, fragmentId, definitionId, position);
        if (edge is null) return new(created, false);
        if (definitionId == StoryNodeIds.parallel)
        {
            var scope = codec.Read<ParallelNode>(draft.graph.document.FindNode(created)!);
            edits.Connect(draft, edge.output, created); edits.Connect(draft, new(new(scope.joinId), new("next")), edge.input.nodeId);
            return new(created, true);
        }
        if (definitionId == StoryNodeIds.choice)
        {
            var choice = codec.Read<ChoiceNode>(draft.graph.document.FindNode(created)!);
            foreach (var option in choice.options)
            {
                string fragment = Guid.NewGuid().ToString("N");
                draft.fragments = [.. draft.fragments, new() { id = fragment, name = option.text }];
                GraphNodeId body = edits.Add(draft, fragment, StoryNodeIds.dialogue, new DialogueNode
                { textId = Guid.NewGuid().ToString("N"), text = option.text + "之后的对白" }, new(position.x + (option.id == choice.options[0].id ? -150 : 150), position.y + 160));
                edits.Connect(draft, new(created, new(option.id)), body); edits.Connect(draft, new(body, new("next")), edge.input.nodeId);
            }
            edits.Connect(draft, edge.output, created); return new(created, true);
        }
        var node = draft.graph.document.FindNode(created)!;
        var catalog = new StoryNodeCatalog(codec); _ = catalog.TryResolve(definitionId, out var declaration);
        var ports = declaration!.GetPorts(node);
        if (ports.Count(static port => port.direction == GraphPortDirection.Output) == 1
            && ports.Any(static port => port.direction == GraphPortDirection.Input && port.id.value == "in")
            && ports.Any(static port => port.direction == GraphPortDirection.Output && port.id.value == "next"))
        { edits.Insert(draft, edge.id, created); return new(created, true); }
        return new(created, false);
    }
}

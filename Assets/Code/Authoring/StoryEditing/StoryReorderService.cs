using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Graphs;
using InnoEngine.Diagnostics;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Authoring.Diagnostics;

namespace Inno.VisualNovel.Authoring.StoryEditing;

/// <summary>Prepares and applies topology-aware card moves over the one authoritative native graph.</summary>
public sealed class StoryReorderService(
    IStorySerialization serialization,
    StoryNodeDataCodec codec,
    StoryCompiler compiler
)
{
    private readonly StoryNodeCatalog m_catalog = new(codec);
    /// <summary>Resolves a card drop to its single actual incoming or outgoing connection without compiling or changing the graph.</summary>
    /// <param name="draft">The current authoritative chapter draft.</param>
    /// <param name="node">The ordinary instruction being moved.</param>
    /// <param name="target">The hovered existing card, distinct from the moving instruction.</param>
    /// <param name="after">Whether to insert after the target rather than before it.</param>
    /// <returns>The precise connection for a subsequent compiler-checked preview; ambiguous branches and merges require an explicit connection instead.</returns>
    public GraphEdgeId DropConnection(
        StoryDraftState draft,
        GraphNodeId node,
        GraphNodeId target,
        bool after
    ) {
        var index = new StoryConnectionIndex(draft, m_catalog);
        if (!index.Linear(node)) throw new AuthoringValidationException($"Moving a card requires one incoming connection and one next output, outside public entries and parallel boundaries.");
        if (node == target || draft.graph.document.FindNode(target) is null)
            throw new AuthoringValidationException($"Choose a different target card that still exists.");
        var edges = draft.graph.document.edges.Where(edge => after ? edge.output.nodeId == target : edge.input.nodeId == target).ToArray();
        if (edges.Length != 1) throw new AuthoringValidationException($"The target has no unique connection in this direction. Drop onto a specific execution connection.");
        var destination = edges[0]; var before = index.Before(node); var next = index.After(node);
        if (destination.id == before.id || destination.id == next.id || before.output.nodeId == node || next.input.nodeId == node)
            throw new AuthoringValidationException($"The instruction is already on this edge, or the connection would reference itself.");
        return destination.id;
    }
    /// <summary>Previews the exact connection changes and validates a detached candidate with the production compiler.</summary>
    /// <param name="draft">The current chapter draft, never mutated by preview.</param>
    /// <param name="node">The ordinary single-input, single-output instruction to move.</param>
    /// <param name="destination">An existing execution connection receiving the moved instruction.</param>
    /// <returns>A stable proposal; invalid scopes, public entries and incomplete candidates throw before mutation.</returns>
    public StoryMovePlan Preview(
        StoryDraftState draft,
        GraphNodeId node,
        GraphEdgeId destination
    ) {
        StoryMovePlan plan = Describe(draft, node, destination);
        var candidate = serialization.Deserialize<StoryDraftState>(serialization.Serialize(draft));
        Rewrite(candidate.graph.document, plan);
        var result = compiler.Compile(candidate.ToAsset());
        if (!result.success) throw new AuthoringValidationException($"The moved story failed compilation: {string.Join("; ", result.diagnostics.Where(static item => item.severity == DiagnosticSeverity.Error).Select(static item => item.message))}");
        return plan;
    }
    /// <summary>Applies an unchanged reviewed proposal inside the caller's shared document transaction.</summary>
    /// <param name="draft">The current detached document candidate.</param>
    /// <param name="plan">The previously displayed connection proposal.</param>
    /// <param name="acceptBoundaryChange">Whether the author explicitly accepted a displayed structural boundary change.</param>
    public void Apply(
        StoryDraftState draft,
        StoryMovePlan plan,
        bool acceptBoundaryChange = false
    ) {
        if (plan.chapter != draft.chapterId) throw new AuthoringValidationException($"The move preview belongs to another chapter.");
        var current = Preview(draft, plan.node, plan.destination);
        if (current.crossesControlBoundary != plan.crossesControlBoundary || !current.changes.SequenceEqual(plan.changes))
            throw new AuthoringValidationException($"Connections changed. Preview the move again.");
        if (plan.crossesControlBoundary && !acceptBoundaryChange) throw new AuthoringValidationException($"Moving across branches, joins or fragments requires explicit acceptance of the connection changes.");
        Rewrite(draft.graph.document, current);
    }
    /// <summary>Finds the insertion connection for swapping with an adjacent ordinary instruction in the same linear segment.</summary>
    /// <param name="draft">The current chapter.</param>
    /// <param name="node">The selected existing instruction.</param>
    /// <param name="later">Whether to move one instruction later rather than earlier.</param>
    /// <returns>The destination connection, or null at a structural boundary.</returns>
    public GraphEdgeId? Adjacent(
        StoryDraftState draft,
        GraphNodeId node,
        bool later
    ) {
        var index = new StoryConnectionIndex(draft, m_catalog);
        if (!index.Linear(node)) return null;
        var edge = later ? index.After(node) : index.Before(node);
        GraphNodeId neighbor = later ? edge.input.nodeId : edge.output.nodeId;
        if (!index.Linear(neighbor) || index.Fragment(node) != index.Fragment(neighbor)) return null;
        return (later ? index.After(neighbor) : index.Before(neighbor)).id;
    }
    private StoryMovePlan Describe(
        StoryDraftState draft,
        GraphNodeId node,
        GraphEdgeId destination
    ) {
        var index = new StoryConnectionIndex(draft, m_catalog);
        if (!index.Linear(node)) throw new AuthoringValidationException($"The moved instruction needs one incoming edge and one next output, and must not be a public entry or parallel boundary.");
        var graph = draft.graph.document;
        var before = index.Before(node); var after = index.After(node);
        var target = graph.edges.SingleOrDefault(edge => edge.id == destination) ?? throw new AuthoringValidationException($"The destination edge no longer exists.");
        if (target.id == before.id || target.id == after.id || before.output.nodeId == node || after.input.nodeId == node)
            throw new AuthoringValidationException($"The instruction is already on this edge, or the connection would reference itself.");
        string fragment = index.Fragment(node);
        var segment = new HashSet<GraphEdgeId> { before.id, after.id };
        Follow(before.output.nodeId, false); Follow(after.input.nodeId, true);
        bool boundary = !segment.Contains(destination) || fragment != index.Fragment(target.output.nodeId) || fragment != index.Fragment(target.input.nodeId);
        return new(draft.chapterId, node, destination, boundary,
        [new(before.id, before.output, before.input, before.output, after.input),
            new(after.id, after.output, after.input, after.output, target.input),
            new(target.id, target.output, target.input, target.output, before.input)]);

        void Follow(
            GraphNodeId current,
            bool forward
        ) {
            var visited = new HashSet<GraphNodeId>();
            while (visited.Add(current) && current != node && index.Linear(current) && index.Fragment(current) == fragment)
            {
                var next = forward ? index.After(current) : index.Before(current);
                segment.Add(next.id); current = forward ? next.input.nodeId : next.output.nodeId;
            }
        }
    }
    private static void Rewrite(
        GraphDocument graph,
        StoryMovePlan plan
    ) {
        foreach (var change in plan.changes) graph.RemoveEdge(change.id);
        foreach (var change in plan.changes) graph.AddEdge(new(change.id, change.afterOutput, change.afterInput));
    }
}

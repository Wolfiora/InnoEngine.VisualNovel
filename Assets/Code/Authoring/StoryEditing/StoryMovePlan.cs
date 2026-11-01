using System;
using System.Collections.Generic;
using InnoEngine.Graphs;

namespace Inno.VisualNovel.Authoring.StoryEditing;

/// <summary>Holds the immutable, compiler-checked connection proposal for moving one existing instruction.</summary>
public sealed class StoryMovePlan
{
    internal StoryMovePlan(
        string chapter,
        GraphNodeId node,
        GraphEdgeId destination,
        bool crossesControlBoundary,
        StoryConnectionChange[] changes
    ) {
        this.chapter = chapter; this.node = node; this.destination = destination;
        this.crossesControlBoundary = crossesControlBoundary; this.changes = Array.AsReadOnly(changes);
    }
    /// <summary>Gets the source chapter identity used to reject cross-document application.</summary>
    public string chapter { get; }
    /// <summary>Gets the existing instruction identity, preserved by the move.</summary>
    public GraphNodeId node { get; }
    /// <summary>Gets the connection on which this instruction will be inserted.</summary>
    public GraphEdgeId destination { get; }
    /// <summary>Gets whether the move changes a branch, merge or fragment boundary and requires explicit review.</summary>
    public bool crossesControlBoundary { get; }
    /// <summary>Gets all three before/after execution connections, preserving their existing identities.</summary>
    public IReadOnlyList<StoryConnectionChange> changes { get; }
}

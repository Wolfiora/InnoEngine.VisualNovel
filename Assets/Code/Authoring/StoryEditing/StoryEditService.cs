using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Graphs;
using InnoEngine.Serialization;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Model.Story.Nodes.Variables;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Model.Story.Nodes.Audio;

namespace Inno.VisualNovel.Authoring.StoryEditing;

/// <summary>Applies topology-aware operations to a detached candidate shared by every authoring view.</summary>
public sealed class StoryEditService
{
    private readonly StoryNodeDataCodec m_codec;
    private readonly StoryNodeCatalog m_catalog;

    /// <summary>Creates the semantic editing boundary.</summary>
    /// <param name="codec">The current native settings codec.</param>
    public StoryEditService(StoryNodeDataCodec codec)
    {
        m_codec = codec ?? throw new ArgumentNullException(nameof(codec));
        m_catalog = new(codec);
    }
    /// <summary>Creates an owned fork, join and editable branches as one detached document operation.</summary>
    /// <param name="draft">The document candidate.</param>
    /// <param name="fragment">The owning fragment.</param>
    /// <param name="position">The fork's graph position.</param>
    /// <returns>The stable fork identity.</returns>
    public GraphNodeId AddParallel(
        StoryDraftState draft,
        string fragment,
        GraphPosition position
    ) => new ParallelEditService(this, m_codec).Create(draft, fragment, position);

    /// <summary>Adds a typed node and its exact fragment ownership.</summary>
    /// <typeparam name="T">The reference-free node settings type.</typeparam>
    /// <param name="draft">The detached candidate.</param>
    /// <param name="fragmentId">The owning fragment.</param>
    /// <param name="definitionId">The stable implementation identity.</param>
    /// <param name="settings">The typed settings.</param>
    /// <param name="position">The graph display position.</param>
    /// <returns>The newly allocated stable node identity.</returns>
    public GraphNodeId Add<T>(
        StoryDraftState draft,
        string fragmentId,
        string definitionId,
        T settings,
        GraphPosition position
    ) where T : class, ISerializable
    {
        GraphNodeId id = Add(draft, fragmentId, definitionId, position);
        GraphNodeRecord node = draft.graph.document.FindNode(id)!;
        m_codec.Write(node, settings);
        return id;
    }

    /// <summary>Adds a node before its typed settings are assigned in the same candidate transaction.</summary>
    /// <param name="draft">The detached candidate.</param>
    /// <param name="fragmentId">The owning fragment.</param>
    /// <param name="definitionId">The registered definition.</param>
    /// <param name="position">A finite graph position.</param>
    /// <returns>The new stable identity.</returns>
    public GraphNodeId Add(
        StoryDraftState draft,
        string fragmentId,
        string definitionId,
        GraphPosition position
    ) {
        StoryFragment fragment = draft.fragments.Single(item => item.id == fragmentId);
        if (!m_catalog.TryResolve(definitionId, out _) || !float.IsFinite(position.x) || !float.IsFinite(position.y))
            throw new ArgumentException("The node definition or position is invalid.");
        var id = new GraphNodeId(Guid.NewGuid().ToString("N"));
        draft.graph.document.AddNode(new(id, definitionId) { position = position });
        fragment.nodes = [.. fragment.nodes, id.value];
        return id;
    }

    /// <summary>Updates settings without silently discarding connections from removed dynamic ports.</summary>
    /// <typeparam name="T">The native node payload.</typeparam>
    /// <param name="draft">The candidate document.</param>
    /// <param name="id">The owning node.</param>
    /// <param name="settings">The replacement payload.</param>
    /// <param name="disconnectRemoved">Whether removal of the displayed affected connections was explicitly accepted.</param>
    public void SetSettings<T>(
        StoryDraftState draft,
        GraphNodeId id,
        T settings,
        bool disconnectRemoved = false
    ) where T : class, ISerializable
    {
        GraphNodeRecord node = draft.graph.document.FindNode(id) ?? throw new ArgumentException("Node is missing.", nameof(id));
        if (!m_catalog.TryResolve(node.definitionId, out GraphNodeDefinition? definition))
            throw new InvalidOperationException("Missing implementations retain their original payload.");
        bool compatible = settings switch
        {
            Model.Story.Nodes.Debugging.LogNode => node.definitionId == StoryNodeIds.log,
            ParallelNode => node.definitionId == StoryNodeIds.parallel,
            JoinNode => node.definitionId == StoryNodeIds.join,
            Model.Story.Nodes.Extensions.CommandNode => node.definitionId == StoryNodeIds.command,
            UnlockNode => node.definitionId == StoryNodeIds.unlock,
            Model.Story.Nodes.Interface.SystemNode => node.definitionId == StoryNodeIds.system,
            StageNode => node.definitionId == StoryNodeIds.stage,
            ScreenEffectNode => node.definitionId == StoryNodeIds.screenEffect,
            TransitionNode => node.definitionId == StoryNodeIds.transition,
            PerformanceNode => node.definitionId == StoryNodeIds.performance,
            AudioNode => node.definitionId == StoryNodeIds.audio,
            DialogueNode => node.definitionId == StoryNodeIds.dialogue,
            InputNode => node.definitionId == StoryNodeIds.input,
            ChoiceNode => node.definitionId == StoryNodeIds.choice,
            AssignmentNode => node.definitionId == StoryNodeIds.assign,
            ConditionNode => node.definitionId is StoryNodeIds.condition or StoryNodeIds.assertion,
            WaitNode => node.definitionId == StoryNodeIds.wait,
            CallNode => node.definitionId == StoryNodeIds.call,
            RandomNode => node.definitionId == StoryNodeIds.random,
            _ => false
        };
        if (!compatible) throw new ArgumentException("The payload does not belong to this node definition.", nameof(settings));
        var candidate = new GraphNodeRecord(id, node.definitionId);
        m_codec.Write(candidate, settings);
        var outputs = definition!.GetPorts(candidate).Where(static port => port.direction == GraphPortDirection.Output).Select(static port => port.id).ToHashSet();
        GraphEdgeId[] removed = draft.graph.document.edges.Where(edge => edge.output.nodeId == id && !outputs.Contains(edge.output.portId)).Select(static edge => edge.id).ToArray();
        if (removed.Length > 0 && !disconnectRemoved)
            throw new InvalidOperationException("Removing an output requires an explicit decision about its connections.");
        m_codec.Write(node, settings);
        foreach (GraphEdgeId edge in removed) draft.graph.document.RemoveEdge(edge);
    }

    /// <summary>Disconnects exactly one output without affecting incoming merge paths.</summary>
    /// <param name="draft">The candidate document.</param>
    /// <param name="output">The output to clear.</param>
    public void Disconnect(
        StoryDraftState draft,
        GraphEndpoint output
    ) {
        foreach (GraphEdgeId edge in draft.graph.document.edges.Where(edge => edge.output == output).Select(static edge => edge.id).ToArray())
            draft.graph.document.RemoveEdge(edge);
    }

    /// <summary>Connects an execution output while retaining all other incoming paths at the destination.</summary>
    /// <param name="draft">The candidate document.</param>
    /// <param name="output">The existing output endpoint.</param>
    /// <param name="destination">The destination node identity.</param>
    public void Connect(
        StoryDraftState draft,
        GraphEndpoint output,
        GraphNodeId destination
    ) {
        GraphDocument graph = draft.graph.document;
        GraphNodeRecord source = graph.FindNode(output.nodeId) ?? throw new ArgumentException("Source node is missing.", nameof(output));
        GraphNodeRecord target = graph.FindNode(destination) ?? throw new ArgumentException("Destination node is missing.", nameof(destination));
        if (!m_catalog.TryResolve(source.definitionId, out GraphNodeDefinition? sourceDefinition) || sourceDefinition is null
            || !sourceDefinition.GetPorts(source).Any(port => port.direction == GraphPortDirection.Output && port.id == output.portId))
            throw new ArgumentException("The source output is unavailable.", nameof(output));
        if (!m_catalog.TryResolve(target.definitionId, out GraphNodeDefinition? targetDefinition) || targetDefinition is null
            || !targetDefinition.GetPorts(target).Any(static port => port.direction == GraphPortDirection.Input && port.id.value == "in"))
            throw new ArgumentException("The destination cannot accept execution.", nameof(destination));
        GraphEdgeId[] replaced = graph.edges.Where(edge => edge.output == output).Select(static edge => edge.id).ToArray();
        foreach (GraphEdgeId id in replaced)
            graph.RemoveEdge(id);
        graph.AddEdge(new(new(Guid.NewGuid().ToString("N")), output, new(destination, new("in"))));
    }

    /// <summary>Inserts a node into an existing execution edge without changing other paths.</summary>
    /// <param name="draft">The candidate document.</param>
    /// <param name="edgeId">The existing execution edge.</param>
    /// <param name="inserted">The existing unconnected inserted node.</param>
    public void Insert(
        StoryDraftState draft,
        GraphEdgeId edgeId,
        GraphNodeId inserted
    ) {
        GraphEdgeRecord edge = draft.graph.document.edges.Single(item => item.id == edgeId);
        GraphNodeRecord node = draft.graph.document.FindNode(inserted) ?? throw new ArgumentException("Inserted node is missing.", nameof(inserted));
        if (!m_catalog.TryResolve(node.definitionId, out GraphNodeDefinition? definition))
            throw new InvalidOperationException("Inserted node implementation is missing.");
        if (definition!.GetPorts(node).Count(port => port.direction == GraphPortDirection.Output) != 1
            || !definition.GetPorts(node).Any(static port => port.id.value == "next"))
            throw new InvalidOperationException("Only a linear node can be inserted into one edge.");
        if (draft.graph.document.edges.Any(item => item.input.nodeId == inserted || item.output.nodeId == inserted))
            throw new InvalidOperationException("The inserted node is already connected.");
        Connect(draft, edge.output, inserted);
        Connect(draft, new(inserted, new("next")), edge.input.nodeId);
    }

    /// <summary>Continues a dialogue's single execution edge with a fresh empty line retaining its writing style.</summary>
    /// <param name="draft">The detached candidate owned by the shared document transaction.</param>
    /// <param name="current">The existing dialogue whose next edge is explicitly selected.</param>
    /// <returns>A fresh stable node with a new text identity and no inherited voice recording.</returns>
    public GraphNodeId ContinueDialogue(
        StoryDraftState draft,
        GraphNodeId current
    ) {
        var node = draft.graph.document.FindNode(current) ?? throw new InvalidOperationException("The dialogue no longer exists.");
        var outgoing = draft.graph.document.edges.Where(edge => edge.output.nodeId == current).ToArray();
        var fragments = draft.fragments.Where(fragment => fragment.nodes.Contains(current.value, StringComparer.Ordinal)).ToArray();
        if (node.definitionId != StoryNodeIds.dialogue || outgoing.Length != 1 || outgoing[0].output.portId.value != "next" || fragments.Length != 1
            || draft.graph.document.FindNode(outgoing[0].input.nodeId) is null)
            throw new InvalidOperationException("Continue writing from an owned dialogue with one valid next connection.");
        var line = m_codec.Read<DialogueNode>(node);
        line.textId = Guid.NewGuid().ToString("N"); line.text = string.Empty; line.voiceCueId = string.Empty; line.clearPage = false;
        GraphNodeId created = Add(draft, fragments[0].id, StoryNodeIds.dialogue, line, new(node.position.x + 320, node.position.y));
        Insert(draft, outgoing[0].id, created); return created;
    }

    /// <summary>Deletes nodes, requiring explicit topology loss for complex junctions.</summary>
    /// <param name="draft">The candidate document.</param>
    /// <param name="id">The node to remove.</param>
    /// <param name="disconnectComplex">Whether the caller explicitly accepted removal of complex connections.</param>
    public void Remove(
        StoryDraftState draft,
        GraphNodeId id,
        bool disconnectComplex = false
    ) {
        GraphDocument graph = draft.graph.document;
        if (draft.entries.Any(entry => entry.nodeId == id.value))
            throw new InvalidOperationException("Reassign or remove public entries before deleting their target.");
        GraphEdgeRecord[] incoming = graph.edges.Where(edge => edge.input.nodeId == id).ToArray();
        GraphEdgeRecord[] outgoing = graph.edges.Where(edge => edge.output.nodeId == id).ToArray();
        if ((incoming.Length > 1 || outgoing.Length > 1) && !disconnectComplex)
            throw new InvalidOperationException("Deleting a junction requires an explicit connection decision.");
        bool bridge = incoming.Length == 1 && outgoing.Length == 1 && outgoing[0].input.nodeId != id && incoming[0].output.nodeId != id;
        if (!graph.RemoveNode(id))
            throw new ArgumentException("Node is missing.", nameof(id));
        foreach (StoryFragment fragment in draft.fragments)
            fragment.nodes = fragment.nodes.Where(value => value != id.value).ToArray();
        if (bridge)
            Connect(draft, incoming[0].output, outgoing[0].input.nodeId);
    }

    /// <summary>Updates node layout independently of execution order.</summary>
    /// <param name="draft">The candidate document.</param>
    /// <param name="positions">Stable node identities and their finite display positions.</param>
    public void Move(
        StoryDraftState draft,
        IReadOnlyDictionary<GraphNodeId, GraphPosition> positions
    ) {
        foreach ((GraphNodeId id, GraphPosition point) in positions)
        {
            if (draft.graph.document.FindNode(id) is null || !float.IsFinite(point.x) || !float.IsFinite(point.y))
                throw new ArgumentException("Movement contains a missing node or invalid coordinate.", nameof(positions));
        }
        foreach ((GraphNodeId id, GraphPosition point) in positions)
            draft.graph.document.FindNode(id)!.position = point;
    }
}

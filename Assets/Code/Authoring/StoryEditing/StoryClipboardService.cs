using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Graphs;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Authoring.StoryEditing;

/// <summary>Copies neutral graph selections and remaps node, option and line identities without a second story representation.</summary>
/// <param name="serialization">The generation-owned native serializer.</param>
/// <param name="codec">The native node settings owner.</param>
public sealed class StoryClipboardService(
    IStorySerialization serialization,
    StoryNodeDataCodec codec
)
{
    /// <summary>Describes copied membership and external destinations through the same native clipboard contract used by paste.</summary>
    /// <param name="bytes">The complete native selection buffer.</param>
    /// <returns>An immutable review; no graph or document is changed.</returns>
    public StoryClipboardReview Review(ReadOnlySpan<byte> bytes)
    {
        var state = serialization.Deserialize<StoryClipboardState>(bytes);
        if (state.graph.document.nodes.Count is < 1 or > 10000) throw new InvalidOperationException("Clipboard node budget is invalid.");
        return new(state);
    }
    /// <summary>Captures selected nodes, outgoing connections and conservatively referenced declarations.</summary>
    /// <param name="draft">The source chapter.</param>
    /// <param name="nodeIds">The selected stable node identities.</param>
    /// <returns>Native clipboard bytes safe to retain across editor generations.</returns>
    public byte[] Capture(
        StoryDraftState draft,
        IReadOnlyCollection<string> nodeIds
    ) {
        var ids = nodeIds.ToHashSet(StringComparer.Ordinal);
        if (ids.Count == 0 || ids.Any(id => draft.graph.document.FindNode(new(id)) is null)) throw new ArgumentException("Clipboard selection contains no nodes or missing identities.");
        var state = new StoryClipboardState { chapterId = draft.chapterId };
        foreach (GraphNodeRecord node in draft.graph.document.nodes.Where(node => ids.Contains(node.id.value))) state.graph.document.AddNode(Copy(node, node.id, new(0, 0)));
        foreach (GraphEdgeRecord edge in draft.graph.document.edges.Where(edge => ids.Contains(edge.output.nodeId.value)))
            state.graph.document.AddEdge(new(edge.id, edge.output, edge.input));
        var variables = new VariableEditService(codec);
        state.variables = draft.GetVariables().Where(variable => variables.FindUsages(draft, variable.id).Any(ids.Contains)).ToArray();
        return serialization.Serialize(state);
    }
    /// <summary>Pastes fresh identities while retaining unresolved external destinations explicitly as Missing.</summary>
    /// <param name="draft">The destination chapter candidate.</param>
    /// <param name="fragmentId">The owning destination fragment.</param>
    /// <param name="bytes">Native selection bytes.</param>
    /// <param name="offset">The finite graph layout offset.</param>
    /// <param name="externalTargets">Optional explicit source-target remapping for cross-chapter links.</param>
    /// <param name="variableTargets">Optional explicit variable destinations; existing declarations retain their defaults and scope, new IDs import the source declaration.</param>
    /// <returns>The new node identities in the copied selection's stable order.</returns>
    public string[] Paste(
        StoryDraftState draft,
        string fragmentId,
        ReadOnlySpan<byte> bytes,
        GraphPosition offset,
        IReadOnlyDictionary<string, string>? externalTargets = null,
        IReadOnlyDictionary<string, string>? variableTargets = null
    ) {
        if (!float.IsFinite(offset.x) || !float.IsFinite(offset.y)) throw new ArgumentException("Paste layout offset must be finite.");
        StoryFragment fragment = draft.fragments.Single(item => item.id == fragmentId);
        StoryClipboardState source = serialization.Deserialize<StoryClipboardState>(bytes);
        if (source.graph.document.nodes.Count == 0 || source.graph.document.nodes.Count > 10000) throw new InvalidOperationException("Clipboard node budget is invalid.");
        var variableCandidates = draft.variables.ToList();
        var effectiveVariables = draft.GetVariables().ToDictionary(static item => item.id, StringComparer.Ordinal);
        if (variableTargets is { Count: > 0 }) new StoryClipboardVariables(codec).Remap(source, variableTargets, effectiveVariables);
        foreach (VariableDefinition variable in source.variables)
        {
            VariableDefinition? existing = effectiveVariables.GetValueOrDefault(variable.id);
            if (existing is null) { variableCandidates.Add(variable); effectiveVariables.Add(variable.id, variable); }
            else if (!StoryVariableDeclarations.Equivalent(existing, variable))
                throw new InvalidOperationException("Resolve the conflicting copied variable before pasting: " + variable.id);
        }
        var nodes = source.graph.document.nodes.ToDictionary(static node => node.id.value, static _ => Guid.NewGuid().ToString("N"), StringComparer.Ordinal);
        var ports = new Dictionary<(string node, string port), string>();
        var copies = new List<GraphNodeRecord>();
        foreach (GraphNodeRecord node in source.graph.document.nodes)
        {
            GraphNodeRecord copy = Copy(node, new(nodes[node.id.value]), offset);
            if (!float.IsFinite(copy.position.x) || !float.IsFinite(copy.position.y)) throw new InvalidOperationException("Pasted coordinates exceed the graph range.");
            if (copy.definitionId == StoryNodeIds.dialogue)
            {
                DialogueNode line = codec.Read<DialogueNode>(copy); line.textId = Guid.NewGuid().ToString("N"); codec.Write(copy, line);
            }
            else if (copy.definitionId == StoryNodeIds.parallel)
            {
                var parallel = codec.Read<Model.Story.Nodes.Flow.ParallelNode>(copy);
                parallel.joinId = nodes.GetValueOrDefault(parallel.joinId) ?? "missing/copied-join/" + Guid.NewGuid().ToString("N");
                foreach (string branch in parallel.branches) ports[(node.id.value, branch)] = Guid.NewGuid().ToString("N");
                parallel.branches = parallel.branches.Select(branch => ports[(node.id.value, branch)]).ToArray(); codec.Write(copy, parallel);
            }
            else if (copy.definitionId == StoryNodeIds.join)
            {
                var join = codec.Read<Model.Story.Nodes.Flow.JoinNode>(copy); join.forkId = nodes.GetValueOrDefault(join.forkId) ?? "missing/copied-fork/" + Guid.NewGuid().ToString("N"); codec.Write(copy, join);
            }
            else if (copy.definitionId == StoryNodeIds.choice)
            {
                ChoiceNode choice = codec.Read<ChoiceNode>(copy);
                foreach (ChoiceOption option in choice.options)
                {
                    string original = option.id; option.id = Guid.NewGuid().ToString("N"); ports.Add((node.id.value, original), option.id);
                    if (choice.defaultOption == original) choice.defaultOption = option.id;
                }
                codec.Write(copy, choice);
            }
            else if (copy.definitionId == StoryNodeIds.random)
            {
                RandomNode random = codec.Read<RandomNode>(copy);
                foreach (WeightedBranch branch in random.branches) { string original = branch.id; branch.id = Guid.NewGuid().ToString("N"); ports.Add((node.id.value, original), branch.id); }
                codec.Write(copy, random);
            }
            copies.Add(copy);
        }
        var edges = new List<GraphEdgeRecord>();
        foreach (GraphEdgeRecord edge in source.graph.document.edges)
        {
            string target;
            if (nodes.TryGetValue(edge.input.nodeId.value, out string? internalTarget)) target = internalTarget;
            else if (externalTargets?.TryGetValue(edge.input.nodeId.value, out string? mapped) == true)
            {
                if (draft.graph.document.FindNode(new(mapped)) is null) throw new InvalidOperationException("The explicitly mapped paste target is missing: " + mapped);
                target = mapped;
            }
            else if (source.chapterId == draft.chapterId && draft.graph.document.FindNode(edge.input.nodeId) is not null) target = edge.input.nodeId.value;
            else target = "missing/copied/" + Guid.NewGuid().ToString("N") + "/" + source.chapterId + "/" + edge.input.nodeId.value;
            string port = ports.GetValueOrDefault((edge.output.nodeId.value, edge.output.portId.value)) ?? edge.output.portId.value;
            edges.Add(new(new(Guid.NewGuid().ToString("N")), new(new(nodes[edge.output.nodeId.value]), new(port)), new(new(target), edge.input.portId)));
        }
        foreach (GraphNodeRecord copy in copies) draft.graph.document.AddNode(copy);
        foreach (GraphEdgeRecord edge in edges) draft.graph.document.AddEdge(edge);
        draft.variables = variableCandidates.ToArray();
        string[] result = copies.Select(static node => node.id.value).ToArray(); fragment.nodes = [.. fragment.nodes, .. result]; return result;
    }
    private static GraphNodeRecord Copy(
        GraphNodeRecord source,
        GraphNodeId id,
        GraphPosition offset
    ) {
        var result = new GraphNodeRecord(id, source.definitionId) { position = new(source.position.x + offset.x, source.position.y + offset.y) };
        foreach (var value in source.values) result.SetValue(value.Key, new GraphSerializedValue(value.Value.data.Span));
        return result;
    }
}

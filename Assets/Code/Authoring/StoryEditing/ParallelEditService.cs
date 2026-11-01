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

/// <summary>Creates and edits owned parallel scopes as ordinary graph transactions shared by both authoring views.</summary>
public sealed class ParallelEditService(
    StoryEditService edits,
    StoryNodeDataCodec codec
)
{
    /// <summary>Creates a fork, its matching join and two editable timed branches.</summary>
    /// <param name="draft">The detached graph candidate.</param>
    /// <param name="fragment">The owning fragment.</param>
    /// <param name="position">The fork's display position.</param>
    /// <returns>The new stable fork identity.</returns>
    public GraphNodeId Create(
        StoryDraftState draft,
        string fragment,
        GraphPosition position
    ) {
        var fork = edits.Add(draft, fragment, StoryNodeIds.parallel, new ParallelNode(), position);
        var join = edits.Add(draft, fragment, StoryNodeIds.join, new JoinNode { forkId = fork.value }, new(position.x + 560, position.y));
        var definition = new ParallelNode { joinId = join.value };
        edits.SetSettings(draft, fork, definition);
        for (int index = 0; index < definition.branches.Length; index++) AddWait(draft, fragment, fork, definition, definition.branches[index], new(position.x + 280, position.y - 90 + index * 180));
        return fork;
    }
    /// <summary>Appends one independently identified timed branch to an existing scope.</summary>
    /// <param name="draft">The detached candidate.</param>
    /// <param name="fork">The owning fork.</param>
    /// <returns>The new output port identity.</returns>
    public string AddBranch(
        StoryDraftState draft,
        GraphNodeId fork
    ) {
        var node = draft.graph.document.FindNode(fork) ?? throw new InvalidOperationException("Parallel fork is missing.");
        var definition = codec.Read<ParallelNode>(node);
        if (definition.branches.Length >= 16) throw new AuthoringValidationException($"并行组最多 16 个分支。");
        string id = Guid.NewGuid().ToString("N"); definition.branches = [.. definition.branches, id]; edits.SetSettings(draft, fork, definition);
        string fragment = draft.fragments.Single(item => item.nodes.Contains(fork.value, StringComparer.Ordinal)).id;
        AddWait(draft, fragment, fork, definition, id, new(node.position.x + 280, node.position.y + definition.branches.Length * 140)); return id;
    }
    /// <summary>Lists branch-owned nodes for an explicit deletion preview.</summary>
    /// <param name="draft">The candidate document.</param>
    /// <param name="fork">The stable fork identity.</param>
    /// <param name="branch">The selected branch port.</param>
    /// <returns>Stable branch members; the shared join is excluded.</returns>
    public string[] Members(
        StoryDraftState draft,
        GraphNodeId fork,
        string branch
    ) {
        var definition = codec.Read<ParallelNode>(draft.graph.document.FindNode(fork) ?? throw new InvalidOperationException("Parallel fork is missing."));
        var first = draft.graph.document.edges.SingleOrDefault(edge => edge.output.nodeId == fork && edge.output.portId.value == branch)?.input.nodeId.value;
        if (first is null) return [];
        var members = new HashSet<string>(StringComparer.Ordinal); var pending = new Stack<string>(); pending.Push(first);
        while (pending.TryPop(out string? id))
        {
            if (id == definition.joinId) continue;
            if (id == fork.value) throw new AuthoringValidationException($"分支越界，先修复返回并行起点的连接。");
            if (!members.Add(id)) continue;
            foreach (var edge in draft.graph.document.edges.Where(edge => edge.output.nodeId.value == id)) pending.Push(edge.input.nodeId.value);
        }
        return members.Order(StringComparer.Ordinal).ToArray();
    }
    /// <summary>Removes a confirmed exclusive branch and its nodes without guessing external reconnections.</summary>
    /// <param name="draft">The detached graph candidate.</param>
    /// <param name="fork">The owning fork.</param>
    /// <param name="branch">The output to remove.</param>
    public void RemoveBranch(
        StoryDraftState draft,
        GraphNodeId fork,
        string branch
    ) {
        var definition = codec.Read<ParallelNode>(draft.graph.document.FindNode(fork) ?? throw new InvalidOperationException("Parallel fork is missing."));
        if (definition.branches.Length <= 2 || !definition.branches.Contains(branch, StringComparer.Ordinal)) throw new AuthoringValidationException($"并行组至少保留两个分支。");
        var members = Members(draft, fork, branch).ToHashSet(StringComparer.Ordinal);
        if (draft.entries.Any(entry => members.Contains(entry.nodeId)) || draft.graph.document.edges.Any(edge => members.Contains(edge.input.nodeId.value)
            && !members.Contains(edge.output.nodeId.value) && !(edge.output.nodeId == fork && edge.output.portId.value == branch)))
            throw new AuthoringValidationException($"分支包含公开入口或外部连接；请先明确重连这些引用。");
        foreach (string id in members) draft.graph.document.RemoveNode(new(id));
        foreach (var fragment in draft.fragments) fragment.nodes = fragment.nodes.Where(id => !members.Contains(id)).ToArray();
        definition.branches = definition.branches.Where(id => id != branch).ToArray(); edits.SetSettings(draft, fork, definition, true);
    }
    /// <summary>Changes deterministic branch scheduling order without changing ports, members or execution edges.</summary>
    /// <param name="draft">The detached shared graph candidate.</param>
    /// <param name="fork">The owning parallel instruction.</param>
    /// <param name="branch">The stable branch port identity.</param>
    /// <param name="index">The branch's final zero-based scheduling position.</param>
    public void MoveBranch(
        StoryDraftState draft,
        GraphNodeId fork,
        string branch,
        int index
    ) {
        var definition = codec.Read<ParallelNode>(draft.graph.document.FindNode(fork) ?? throw new InvalidOperationException("Parallel fork is missing."));
        if (index < 0 || index >= definition.branches.Length || !definition.branches.Contains(branch, StringComparer.Ordinal))
            throw new ArgumentException("The branch or final scheduling position is invalid.");
        var ordered = definition.branches.Where(item => item != branch).ToList(); ordered.Insert(index, branch);
        definition.branches = ordered.ToArray(); edits.SetSettings(draft, fork, definition);
    }
    private void AddWait(
        StoryDraftState draft,
        string fragment,
        GraphNodeId fork,
        ParallelNode definition,
        string branch,
        GraphPosition position
    ) {
        var wait = edits.Add(draft, fragment, StoryNodeIds.wait, new WaitNode { seconds = 0.25 }, position);
        edits.Connect(draft, new(fork, new(branch)), wait); edits.Connect(draft, new(wait, new("next")), new(definition.joinId));
    }
}

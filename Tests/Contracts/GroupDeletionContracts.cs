using System;
using System.Collections.Generic;
using System.Linq;
using Inno.Core.Graphs;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Narrative.Execution;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class GroupDeletionContracts
{
    internal static void LinearReviewNativeIdentityAndStaleness(
        IStorySerialization native,
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var draft = FirstStoryTemplate.Create(codec); var edits = new StoryEditService(codec); var deletion = new StoryDeleteService(edits, codec);
        var second = edits.ContinueDialogue(draft, new("hello"));
        var incoming = draft.graph.document.edges.Single(static edge => edge.output.nodeId.value == "entry");
        byte[] before = native.Serialize(draft); var retained = draft.graph.document.nodes.Where(node => node.id.value != "hello" && node.id != second).Select(static node => (node.id, node.position)).ToArray();
        var review = deletion.Preview(draft, ["hello", second.value]);
        Check(review.nodes.Count == 2 && review.connections.Count == 3 && review.inputs.Single().suggestedTarget == "choice"
            && native.Serialize(draft).SequenceEqual(before), "Deletion review changed its source or failed to find the exact ordinary linear continuation.");
        deletion.Apply(draft, review);
        var replaced = draft.graph.document.edges.Single(edge => edge.id == incoming.id);
        Check(replaced.input.nodeId.value == "choice" && draft.graph.document.FindNode(new("hello")) is null && draft.graph.document.FindNode(second) is null
            && draft.fragments.All(fragment => !fragment.nodes.Contains("hello") && !fragment.nodes.Contains(second.value)), "Group deletion lost stable incoming edge identity or retained removed ownership.");
        foreach (var node in retained) Check(draft.graph.document.FindNode(node.id)?.position == node.position, "Deletion moved an unrelated node.");
        draft = native.Deserialize<StoryDraftState>(native.Serialize(draft));
        using (var machine = new StoryMachine(Compile(compiler, draft))) { machine.Start(); Check(machine.status == StoryStatus.Choice, "Native reopened group deletion did not execute its reviewed continuation."); }

        draft = FirstStoryTemplate.Create(codec); review = deletion.Preview(draft, ["hello"]);
        edits.Connect(draft, new(new("entry"), new("next")), new("sea")); before = native.Serialize(draft);
        Reject(() => deletion.Apply(draft, review)); Check(native.Serialize(draft).SequenceEqual(before), "Stale deletion partially removed newer graph data.");
        Reject(() => deletion.Preview(draft, ["entry"]));
        draft.entries = [.. draft.entries, new() { id = "protected-line", nodeId = "hello" }];
        Reject(() => deletion.Preview(draft, ["hello"]));
        Check(native.Serialize(draft).Length > 0, "Protected entry review damaged the source document.");
    }

    internal static void ExplicitJunctionsCyclesAndParallelScopes(
        IStorySerialization native,
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var edits = new StoryEditService(codec); var deletion = new StoryDeleteService(edits, codec); var draft = FirstStoryTemplate.Create(codec);
        var review = deletion.Preview(draft, ["choice"]); byte[] before = native.Serialize(draft);
        Check(review.inputs.Single().suggestedTarget.Length == 0, "A branching deletion guessed its replacement target.");
        Reject(() => deletion.Apply(draft, review));
        Reject(() => deletion.Apply(draft, review, new Dictionary<GraphEdgeId, string> { [review.inputs[0].id] = "choice" }));
        Check(native.Serialize(draft).SequenceEqual(before), "An unresolved or self-targeting junction decision changed source state.");
        deletion.Apply(draft, review, new Dictionary<GraphEdgeId, string> { [review.inputs[0].id] = "sea" });
        using (var machine = new StoryMachine(Compile(compiler, draft))) { machine.Start(); machine.Advance(); machine.Advance(); Check(machine.sourceNodeId == "sea", "Explicit junction deletion ignored the selected route."); }

        draft = FirstStoryTemplate.Create(codec); edits.Connect(draft, new(new("forest"), new("next")), new("sea"));
        review = deletion.Preview(draft, ["sea"]);
        Check(review.inputs.Count == 2 && review.inputs.All(static input => input.suggestedTarget.Length == 0), "Deleting a merge guessed incoming reconnections.");
        deletion.Apply(draft, review, review.inputs.ToDictionary(static input => input.id, static _ => "end")); _ = Compile(compiler, draft);

        draft = FirstStoryTemplate.Create(codec); edits.Connect(draft, new(new("hello"), new("next")), new("hello"));
        review = deletion.Preview(draft, ["hello"]); Reject(() => deletion.Apply(draft, review));
        deletion.Apply(draft, review, new Dictionary<GraphEdgeId, string> { [review.inputs[0].id] = "end" });
        using (var machine = new StoryMachine(Compile(compiler, draft))) { machine.Start(); Check(machine.status == StoryStatus.Completed, "Explicit loop deletion left an invisible old execution edge."); }

        draft = FirstStoryTemplate.Create(codec); review = deletion.Preview(draft, ["choice"]);
        deletion.Apply(draft, review, new Dictionary<GraphEdgeId, string> { [review.inputs[0].id] = string.Empty });
        Check(!compiler.Compile(draft.ToAsset()).success && !draft.graph.document.edges.Any(static edge => edge.input.nodeId.value == "choice"),
            "Explicit disconnect was executable without diagnostics or retained dangling deleted targets.");

        draft = Parallel(codec, edits, out var fork, out var join); before = native.Serialize(draft);
        Reject(() => deletion.Preview(draft, [fork.value])); Reject(() => deletion.Preview(draft, [join]));
        Check(native.Serialize(draft).SequenceEqual(before), "A rejected partial parallel deletion lost group state.");
        var expanded = deletion.ExpandBoundaries(draft, [fork.value]); Check(expanded.Count == 4 && expanded.Contains(join), "Parallel boundary expansion lost a fork, join or owned branch.");
        review = deletion.Preview(draft, expanded); Check(review.inputs.Single().suggestedTarget.Length == 0, "A parallel boundary deletion guessed story intent.");
        deletion.Apply(draft, review, new Dictionary<GraphEdgeId, string> { [review.inputs[0].id] = "hello" });
        Check(expanded.All(id => draft.graph.document.FindNode(new(id)) is null), "Complete parallel deletion retained owned branch nodes.");
        using (var machine = new StoryMachine(Compile(compiler, draft))) { machine.Start(); Check(machine.sourceNodeId == "hello", "Parallel group deletion failed to continue outside the scope."); }

        draft = Parallel(codec, edits, out fork, out join);
        string wait = draft.graph.document.edges.First(edge => edge.output.nodeId == fork).input.nodeId.value;
        review = deletion.Preview(draft, [wait]); Check(review.inputs.Single().suggestedTarget == join, "An ordinary branch wait lost its safe continuation.");
        deletion.Apply(draft, review);
        using (var machine = new StoryMachine(Compile(compiler, draft))) { machine.Start(); machine.Tick(.3); Check(machine.sourceNodeId == "hello", "Deleting one linear branch body produced an invalid empty task scope."); }
    }
    private static StoryDraftState Parallel(
        StoryNodeDataCodec codec,
        StoryEditService edits,
        out GraphNodeId fork,
        out string join
    ) {
        var draft = FirstStoryTemplate.Create(codec); fork = edits.AddParallel(draft, "opening", new(0, 0)); join = codec.Read<ParallelNode>(draft.graph.document.FindNode(fork)!).joinId;
        edits.Connect(draft, new(new("entry"), new("next")), fork); edits.Connect(draft, new(new(join), new("next")), new("hello")); return draft;
    }
    private static Model.Programs.StoryProgram Compile(
        StoryCompiler compiler,
        StoryDraftState draft
    ) {
        var result = compiler.Compile(draft.ToAsset()); Check(result.success, string.Join("; ", result.diagnostics.Select(static issue => issue.message))); return result.program!;
    }
    private static void Reject(Action action)
    { try { action(); } catch (Exception error) when (error is ArgumentException or InvalidOperationException or FormatException) { return; } throw new InvalidOperationException("An unsafe group deletion was accepted."); }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}

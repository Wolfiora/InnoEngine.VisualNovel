using System;
using System.Linq;
using Inno.Core.Graphs;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.ScriptProjection;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Narrative.Execution;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class StoryReorderContracts
{
    internal static void CardDropDestinations(
        IStorySerialization native,
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var draft = FirstStoryTemplate.Create(codec);
        var edits = new StoryEditService(codec); var service = new StoryReorderService(native, codec, compiler);
        var second = edits.ContinueDialogue(draft, new("hello"));
        var destination = service.DropConnection(draft, second, new("hello"), false);
        Check(destination == service.Adjacent(draft, second, false), "Dropping before a card guessed a different execution connection.");
        var plan = service.Preview(draft, second, destination); service.Apply(draft, plan);
        destination = service.DropConnection(draft, second, new("hello"), true);
        Check(destination == service.Adjacent(draft, second, true), "Dropping after a card ignored its actual output.");
        plan = service.Preview(draft, second, destination); service.Apply(draft, plan);
        Check(draft.graph.document.edges.Single(static edge => edge.output.nodeId.value == "entry").input.nodeId.value == "hello",
            "Two opposite card drops did not restore actual graph execution order.");
        byte[] before = native.Serialize(draft);
        Reject(() => service.DropConnection(draft, new("entry"), second, true));
        Reject(() => service.DropConnection(draft, new("choice"), second, true));
        Reject(() => service.DropConnection(draft, second, second, true));
        Reject(() => service.DropConnection(draft, second, new("hello"), true));
        Reject(() => service.DropConnection(draft, second, new("choice"), true));
        Reject(() => service.DropConnection(draft, second, new("end"), false));
        Reject(() => service.DropConnection(draft, second, new("missing-target"), false));
        var sea = draft.graph.document.edges.Single(static edge => edge.output.nodeId.value == "choice" && edge.output.portId.value == "sea").input.nodeId;
        destination = service.DropConnection(draft, second, sea, false);
        Check(service.Preview(draft, second, destination).crossesControlBoundary, "A drop into a branch omitted the existing review policy.");
        Check(native.Serialize(draft).SequenceEqual(before), "Destination inspection or rejected card drops modified the authored graph.");
    }
    internal static void LinearIdentityAndExecution(
        IStorySerialization native,
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var draft = FirstStoryTemplate.Create(codec); var edits = new StoryEditService(codec); var service = new StoryReorderService(native, codec, compiler);
        var second = edits.ContinueDialogue(draft, new("hello"));
        var line = codec.Read<DialogueNode>(draft.graph.document.FindNode(second)!); line.text = "Second"; edits.SetSettings(draft, second, line);
        byte[] before = native.Serialize(draft); var nodes = draft.graph.document.nodes.Select(static node => (node.id, node.definitionId, node.position,
            values: node.values.ToDictionary(static item => item.Key, static item => item.Value.data.ToArray()))).ToArray();
        var edgeIds = draft.graph.document.edges.Select(static edge => edge.id).OrderBy(static id => id.value).ToArray();
        var destination = service.Adjacent(draft, second, false); Check(destination is not null, "An ordinary neighboring dialogue was not movable.");
        var plan = service.Preview(draft, second, destination!.Value);
        Check(!plan.crossesControlBoundary && plan.changes.Count == 3 && native.Serialize(draft).SequenceEqual(before), "Linear move preview changed authored state or lost its exact impact.");
        service.Apply(draft, plan);
        Check(draft.graph.document.edges.Select(static edge => edge.id).OrderBy(static id => id.value).SequenceEqual(edgeIds), "A card move replaced stable execution connection identities.");
        foreach (var expected in nodes)
        {
            var actual = draft.graph.document.FindNode(expected.id)!;
            Check(actual.definitionId == expected.definitionId && actual.position == expected.position && actual.values.Count == expected.values.Count
                && actual.values.All(item => expected.values.TryGetValue(item.Key, out var bytes) && item.Value.data.Span.SequenceEqual(bytes)),
                "Card reordering changed identity, source data or graph-only layout.");
        }
        var restored = native.Deserialize<StoryDraftState>(native.Serialize(draft)); var program = compiler.Compile(restored.ToAsset());
        Check(program.success && ScriptProjectionBuilder.Build(restored).Select(static row => row.nodeId).ToHashSet().SetEquals(nodes.Select(static node => node.id.value)),
            "Native reopen or script projection lost instructions after the move.");
        using var machine = new StoryMachine(program.program!); machine.Start();
        Check(machine.dialogueText == "Second", "Runtime order still followed card display or node array order."); machine.Advance(); machine.Advance();
        Check(machine.sourceNodeId == "hello", "Linear relocation did not preserve the remainder of its original execution segment.");
        Check(service.Adjacent(restored, new("choice"), false) is null, "A branch was exposed as an automatic linear swap.");
    }
    internal static void ReviewedBoundariesAndAtomicRejection(
        IStorySerialization native,
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var draft = FirstStoryTemplate.Create(codec); var edits = new StoryEditService(codec); var service = new StoryReorderService(native, codec, compiler);
        var target = draft.graph.document.edges.Single(static edge => edge.output.nodeId.value == "choice" && edge.output.portId.value == "sea");
        var plan = service.Preview(draft, new("hello"), target.id); byte[] before = native.Serialize(draft);
        Check(plan.crossesControlBoundary, "Moving dialogue into one branch omitted structural review.");
        Reject(() => service.Apply(draft, plan)); Check(native.Serialize(draft).SequenceEqual(before), "Rejected boundary review partially rewired execution.");
        service.Apply(draft, plan, true);
        using (var machine = new StoryMachine(compiler.Compile(draft.ToAsset()).program!))
        { machine.Start(); Check(machine.status == StoryStatus.Choice, "Reviewed branch relocation did not remove the old linear occurrence."); machine.Choose("sea"); Check(machine.sourceNodeId == "hello", "Reviewed branch relocation lost its explicit destination."); }
        draft = FirstStoryTemplate.Create(codec); var extra = edits.ContinueDialogue(draft, new("hello"));
        plan = service.Preview(draft, extra, service.Adjacent(draft, extra, false)!.Value);
        edits.Connect(draft, new(new("entry"), new("next")), new("choice")); before = native.Serialize(draft);
        Reject(() => service.Apply(draft, plan, true)); Check(native.Serialize(draft).SequenceEqual(before), "A stale move changed the newer graph.");
        Reject(() => service.Preview(draft, new("entry"), draft.graph.document.edges.Last().id));

        draft = FirstStoryTemplate.Create(codec); var fork = edits.AddParallel(draft, "opening", new(0, 0));
        var parallel = codec.Read<ParallelNode>(draft.graph.document.FindNode(fork)!);
        edits.Connect(draft, new(new("entry"), new("next")), fork); edits.Connect(draft, new(new(parallel.joinId), new("next")), new("hello"));
        var branches = parallel.branches.Select(branch => draft.graph.document.edges.Single(edge => edge.output.nodeId == fork && edge.output.portId.value == branch)).ToArray();
        var childLine = edits.Add(draft, "opening", StoryNodeIds.dialogue, new DialogueNode { textId = "child-line", text = "Child" }, new(0, 100));
        edits.Insert(draft, branches[0].id, childLine); before = native.Serialize(draft);
        Reject(() => service.Preview(draft, new("hello"), branches[1].id)); Check(native.Serialize(draft).SequenceEqual(before), "An invalid cross-scope move escaped detached compiler validation.");
    }
    private static void Reject(Action action)
    { try { action(); } catch (ArgumentException) { return; } catch (InvalidOperationException) { return; } throw new InvalidOperationException("An invalid or unreviewed card move was accepted."); }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

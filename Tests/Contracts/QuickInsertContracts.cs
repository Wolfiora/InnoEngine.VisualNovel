using System;
using System.Linq;
using Inno.Core.Graphs;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Narrative.Execution;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class QuickInsertContracts
{
    internal static void CompleteChoiceAndParallelContinuations(
        IStorySerialization native,
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var edits = new StoryEditService(codec); var quick = new StoryQuickInsertService(edits, codec); var source = FirstStoryTemplate.Create(codec);
        string[] original = source.graph.document.nodes.Select(static node => node.id.value).ToArray();
        var choice = quick.Create(source, "opening", StoryNodeIds.choice, new(10, 50), quick.NextEdge(source, "entry"));
        Check(choice.connected && source.fragments.Length == 3 && original.All(id => source.graph.document.FindNode(new(id)) is not null),
            "Creating a choice lost original identities or omitted independently editable branch fragments.");
        var declaration = codec.Read<ChoiceNode>(source.graph.document.FindNode(choice.nodeId)!);
        foreach (var option in declaration.options)
        {
            var copy = native.Deserialize<StoryDraftState>(native.Serialize(source));
            using var machine = new StoryMachine(Compile(compiler, copy)); machine.Start();
            Check(machine.status == StoryStatus.Choice && machine.sourceNodeId == choice.nodeId.value, "The inserted choice did not replace the selected continuation.");
            machine.Choose(option.id); string body = machine.sourceNodeId;
            Check(!original.Contains(body, StringComparer.Ordinal) && copy.fragments.Single(fragment => fragment.nodes.Contains(body, StringComparer.Ordinal)).name == option.text,
                "A choice target did not enter its real independent dialogue fragment.");
            machine.Advance(); machine.Advance(); Check(machine.sourceNodeId == "hello", "A quick choice branch failed to merge at the original target.");
        }
        source = FirstStoryTemplate.Create(codec);
        var parallel = quick.Create(source, "opening", StoryNodeIds.parallel, new(30, 80), quick.NextEdge(source, "entry"));
        var group = codec.Read<ParallelNode>(source.graph.document.FindNode(parallel.nodeId)!);
        Check(parallel.connected && source.graph.document.edges.Any(edge => edge.output.nodeId.value == group.joinId && edge.input.nodeId.value == "hello"),
            "Quick parallel creation omitted its owned join continuation.");
        using var running = new StoryMachine(Compile(compiler, source)); running.Start(); running.Tick(1);
        Check(running.status == StoryStatus.Dialogue && running.sourceNodeId == "hello", "The quick parallel group did not execute and rejoin its real story path.");
    }
    internal static void WritingDetachedNodesAndRejectedEdges(
        IStorySerialization native,
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var edits = new StoryEditService(codec); var quick = new StoryQuickInsertService(edits, codec); var source = FirstStoryTemplate.Create(codec);
        var originalLine = codec.Read<DialogueNode>(source.graph.document.FindNode(new("hello"))!); originalLine.characterId = "hero"; originalLine.voiceCueId = "previous-voice";
        edits.SetSettings(source, new("hello"), originalLine);
        var continued = quick.Create(source, "opening", StoryNodeIds.dialogue, new(123, 456), quick.NextEdge(source, "hello"));
        var line = codec.Read<DialogueNode>(source.graph.document.FindNode(continued.nodeId)!);
        Check(continued.connected && line.text.Length == 0 && line.textId != originalLine.textId && line.characterId == "hero" && line.voiceCueId.Length == 0
            && source.graph.document.FindNode(continued.nodeId)!.position == new GraphPosition(123, 456), "Quick writing lost its speaker, fresh text identity, empty body or requested layout.");
        _ = Compile(compiler, source);
        string target = source.graph.document.edges.Single(static edge => edge.output.nodeId.value == "entry").input.nodeId.value;
        var end = quick.Create(source, "opening", StoryNodeIds.end, new(0, 0), quick.NextEdge(source, "entry"));
        Check(!end.connected && source.graph.document.edges.Single(static edge => edge.output.nodeId.value == "entry").input.nodeId.value == target,
            "Creating a terminal instruction silently discarded an existing story path.");
        Check(quick.NextEdge(source, "choice") is null && quick.NextEdge(source, "absent") is null, "Quick creation guessed a branch or missing source continuation.");
        byte[] before = native.Serialize(source);
        Reject(() => quick.Create(source, "opening", StoryNodeIds.entry, new(0, 0)));
        Reject(() => quick.Create(source, "absent", StoryNodeIds.wait, new(0, 0)));
        Reject(() => quick.Create(source, "opening", StoryNodeIds.wait, new(0, 0), new GraphEdgeId("stale")));
        Reject(() => quick.Create(source, "opening", StoryNodeIds.wait, new(0, 0), source.graph.document.edges.First(edge => edge.output.nodeId.value == "choice").id));
        Check(native.Serialize(source).SequenceEqual(before), "A rejected quick insertion partially changed the document candidate.");
    }
    private static Model.Programs.StoryProgram Compile(
        StoryCompiler compiler,
        StoryDraftState draft
    ) {
        var result = compiler.Compile(draft.ToAsset()); Check(result.success, string.Join("; ", result.diagnostics.Select(static issue => issue.message))); return result.program!;
    }
    private static void Reject(Action action)
    { try { action(); } catch (Exception error) when (error is ArgumentException or InvalidOperationException) { return; } throw new InvalidOperationException("Invalid quick insertion was accepted."); }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}

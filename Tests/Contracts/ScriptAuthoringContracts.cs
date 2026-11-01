using System;
using System.Linq;
using Inno.Core.Graphs;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.ScriptProjection;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Narrative.Execution;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class ScriptAuthoringContracts
{
    internal static void ContinueAlongActualEdges(
        IStorySerialization native,
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var draft = FirstStoryTemplate.Create(codec); var edits = new StoryEditService(codec);
        var previous = draft.graph.document.FindNode(new("hello"))!; var original = codec.Read<DialogueNode>(previous);
        original.presentation = DialoguePresentation.Nvl; original.clearPage = true; original.voiceCueId = "prior-recording"; original.charactersPerSecond = 17; codec.Write(previous, original);
        GraphNodeId created = edits.ContinueDialogue(draft, previous.id);
        var next = codec.Read<DialogueNode>(draft.graph.document.FindNode(created)!);
        Check(next.textId != original.textId && next.text.Length == 0 && next.voiceCueId.Length == 0 && !next.clearPage
            && next.speaker == original.speaker && next.presentation == original.presentation && next.charactersPerSecond == 17, "Continue-writing retained a voice/text identity or lost the authored reading style.");
        next.text = "接着写的新台词"; codec.Write(draft.graph.document.FindNode(created)!, next);
        byte[] before = native.Serialize(draft); var rows = ScriptProjectionBuilder.Build(draft).ToArray();
        Check(rows[0].nodeId == "entry" && rows[1].nodeId == "hello" && rows[2].nodeId == created.value && rows[3].nodeId == "choice"
            && !rows[2].startsBlock && rows.Single(row => row.nodeId == "end").startsBlock, "The script ignored an inserted execution edge or unfolded a merge into its predecessor block.");
        Check(native.Serialize(draft).SequenceEqual(before), "A script projection changed authoritative graph data.");
        draft = native.Deserialize<StoryDraftState>(before); var result = compiler.Compile(draft.ToAsset()); Check(result.success, string.Join("; ", result.diagnostics.Select(static issue => issue.message)));
        using var machine = new StoryMachine(result.program!); machine.Start(); machine.Advance(); machine.Advance();
        Check(machine.nodeId == created.value && machine.dialogueText == next.text, "The visual continuation was appended to display order without reconnecting real execution.");
        machine.Advance(); machine.Advance(); Check(machine.status == StoryStatus.Choice, "Continuing a line discarded its original next instruction.");
    }
    internal static void FiniteFreeGraphs(IStorySerialization native)
    {
        var draft = new StoryDraftState { fragments = [new() { id = "first", name = "First", nodes = ["b", "missing", "a", "c"] }, new() { id = "second", name = "Second", nodes = ["z"] }],
            entries = [new() { id = "start", nodeId = "a" }, new() { id = "alternate", nodeId = "c" }] };
        foreach (string id in new[] { "a", "b", "missing", "c", "z", "unowned" }) draft.graph.document.AddNode(new(new(id), "missing.extension"));
        Link("a", "b"); Link("b", "a"); Link("c", "z");
        byte[] before = native.Serialize(draft); var rows = ScriptProjectionBuilder.Build(draft).ToArray();
        Check(rows.Length == 6 && rows.Select(static row => row.nodeId).Distinct().Count() == 6 && rows[0].nodeId == "a" && rows[1].nodeId == "b"
            && rows.Single(static row => row.nodeId == "c").startsBlock && rows.Single(static row => row.nodeId == "z").fragmentId == "second"
            && rows.Any(static row => row.nodeId == "missing") && rows.Any(static row => row.nodeId == "unowned"), "A free-graph projection lost cyclic, multi-entry, cross-fragment, Missing or unowned source nodes.");
        Check(native.Serialize(draft).SequenceEqual(before), "Free-graph projection rewrote memberships or edges.");
        void Link(
            string from,
            string to
        ) => draft.graph.document.AddEdge(new(new(Guid.NewGuid().ToString("N")), new(new(from), new("next")), new(new(to), new("in"))));
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}

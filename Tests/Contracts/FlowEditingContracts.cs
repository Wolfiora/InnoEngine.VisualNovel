using System;
using System.Linq;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Narrative.Execution;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class FlowEditingContracts
{
    internal static void Clipboard(
        IStorySerialization native,
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        StoryDraftState source = FirstStoryTemplate.Create(codec);
        var clipboard = new StoryClipboardService(native, codec);
        byte[] packet = clipboard.Capture(source, ["hello", "choice", "sea", "forest"]);
        string[] copied = clipboard.Paste(source, "opening", packet, new(100, 100));
        Check(copied.Length == 4 && copied.All(id => id is not ("hello" or "choice" or "sea" or "forest")), "Pasted nodes reused source identities.");
        var choice = source.graph.document.nodes.Single(node => copied.Contains(node.id.value) && node.definitionId == StoryNodeIds.choice);
        string[] originalOptions = codec.Read<ChoiceNode>(source.graph.document.FindNode(new("choice"))!).options.Select(static item => item.id).ToArray();
        Check(codec.Read<ChoiceNode>(choice).options.All(option => !originalOptions.Contains(option.id)), "Pasted options reused internal identities.");
        Check(compiler.Compile(source.ToAsset()).success, "Same-chapter paste did not preserve resolvable outgoing connections.");
        var other = FirstStoryTemplate.Create(codec);
        string[] external = clipboard.Paste(other, "opening", packet, new(100, 100));
        Check(other.graph.document.edges.Any(edge => external.Contains(edge.output.nodeId.value) && edge.input.nodeId.value.StartsWith("missing/copied/", StringComparison.Ordinal)), "Cross-chapter paste guessed an unrelated external destination.");
        Check(!compiler.Compile(other.ToAsset()).success, "Unresolved pasted destinations did not remain diagnosable.");
    }
    internal static void CountedLoop(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        StoryEditService edits
    ) {
        var draft = FirstStoryTemplate.Create(codec);
        StoryLoopTemplate loop = new StoryFlowTemplates(codec).AddCountedLoop(draft, "opening", 3, new(0, 400));
        edits.Connect(draft, new(new("entry"), new("next")), new(loop.entryNode));
        edits.Connect(draft, new(new(loop.exitNode), new("next")), new("end"));
        var result = compiler.Compile(draft.ToAsset()); Check(result.success, string.Join(";", result.diagnostics.Select(static item => item.message)));
        var machine = new StoryMachine(result.program!); machine.Start();
        for (int index = 0; index < 3; index++)
        {
            Check(machine.status == StoryStatus.Dialogue && machine.nodeId == loop.bodyNode && machine.GetVariable(loop.counterVariable).integer == index, "Counted-loop graph diverged from its explicit counter and edges.");
            machine.Advance(); machine.Advance();
        }
        Check(machine.status == StoryStatus.Completed && machine.GetVariable(loop.counterVariable).integer == 3, "Counted loop did not terminate at its explicit exit.");
        Check(Authoring.ScriptProjection.ScriptProjectionBuilder.Build(draft).Count == draft.graph.document.nodes.Count, "Loop template introduced a separate or infinitely expanded script projection.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

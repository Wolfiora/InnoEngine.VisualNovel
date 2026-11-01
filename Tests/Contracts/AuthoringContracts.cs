using System;
using System.Linq;
using Inno.Core.Graphs;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class AuthoringContracts
{
    internal static void Factory(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        StoryEditService edits
    ) {
        StoryDraftState draft = FirstStoryTemplate.Create(codec);
        var catalog = new StoryNodeCatalog(codec);
        var factory = new StoryNodeFactory(edits);
        foreach (GraphNodeDefinition definition in catalog.definitions.Where(static definition => definition.id is not (StoryNodeIds.parallel or StoryNodeIds.join)))
        {
            GraphNodeId id = factory.Create(draft, "opening", definition.id, new(0, 0));
            GraphNodeRecord node = draft.graph.document.FindNode(id)!;
            if (definition.id == StoryNodeIds.unlock) edits.SetSettings(draft, id, new Inno.VisualNovel.Model.Story.Nodes.Flow.UnlockNode { entryId = "example" });
            if (definition.id == StoryNodeIds.command) edits.SetSettings(draft, id, new Inno.VisualNovel.Model.Story.Nodes.Extensions.CommandNode { definition = new() { id = "example", title = "Example" } });
            if (definition.id == StoryNodeIds.screenEffect) edits.SetSettings(draft, id, new Inno.VisualNovel.Model.Story.Nodes.Stage.ScreenEffectNode { resourceId = "example" });
            if (definition.id == StoryNodeIds.transition) edits.SetSettings(draft, id, new Inno.VisualNovel.Model.Story.Nodes.Stage.TransitionNode { resourceId = "example" });
            foreach (GraphPortDefinition port in definition.GetPorts(node).Where(static item => item.direction == GraphPortDirection.Output))
                edits.Connect(draft, new(id, port.id), new("end"));
        }
        StoryCompilationResult result = compiler.Compile(draft.ToAsset());
        int expected = 6 + catalog.definitions.Count - 2;
        Check(result.success && result.program!.instructions.Length == expected, string.Join("; ", result.diagnostics.Select(static item => item.message)));
        Check(draft.fragments.Single().nodes.Distinct(StringComparer.Ordinal).Count() == expected, "Node ownership or stable identity was duplicated.");
    }

    internal static void DynamicPorts(
        IStorySerialization native,
        StoryNodeDataCodec codec,
        StoryEditService edits
    ) {
        StoryDraftState draft = FirstStoryTemplate.Create(codec);
        GraphNodeRecord node = draft.graph.document.nodes.Single(item => item.definitionId == StoryNodeIds.choice);
        ChoiceNode choice = codec.Read<ChoiceNode>(node);
        string removedPort = choice.options[0].id;
        choice.options = choice.options.Skip(1).ToArray();
        byte[] before = native.Serialize(draft);
        try { edits.SetSettings(draft, node.id, new Inno.VisualNovel.Model.Story.Nodes.Flow.WaitNode()); throw new Exception("Wrong settings type was accepted."); }
        catch (ArgumentException) { }
        try { edits.SetSettings(draft, node.id, choice); throw new Exception("Connection removal was silently accepted."); }
        catch (InvalidOperationException) { }
        Check(native.Serialize(draft).AsSpan().SequenceEqual(before), "Rejected removal changed the draft.");
        edits.SetSettings(draft, node.id, choice, disconnectRemoved: true);
        Check(!draft.graph.document.edges.Any(edge => edge.output.nodeId == node.id && edge.output.portId.value == removedPort), "Removed output retained an orphan edge.");
        Check(draft.graph.document.edges.Count(edge => edge.input.nodeId.value == "end") == 2, "Unrelated merge paths were changed.");
        edits.Disconnect(draft, new(node.id, new(choice.options[0].id)));
        Check(!draft.graph.document.edges.Any(edge => edge.output.nodeId == node.id), "Explicit output disconnect failed.");
    }

    internal static void StructureAndVariables(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        StoryDraftState draft = FirstStoryTemplate.Create(codec);
        string fingerprint = compiler.Compile(draft.ToAsset()).program!.fingerprint;
        var structure = new StoryStructureService();
        string fragment = structure.AddFragment(draft, "支线");
        structure.MoveToFragment(draft, "hello", fragment);
        Check(draft.fragments.Count(item => item.nodes.Contains("hello", StringComparer.Ordinal)) == 1, "Moving a node duplicated ownership.");
        Check(compiler.Compile(draft.ToAsset()).program!.fingerprint == fingerprint, "Display grouping changed execution.");
        structure.SetEntry(draft, "chapter-preview", "hello");
        Check(draft.entries.Single(item => item.id == "chapter-preview").nodeId == "hello", "Public entry was not registered.");
        var variables = new VariableEditService(codec);
        Check(variables.FindUsages(draft, "player").Contains("hello", StringComparer.Ordinal), "Dialogue interpolation reference was missed.");
        try { variables.Remove(draft, "player"); throw new Exception("Referenced variable removal was accepted."); }
        catch (InvalidOperationException) { }
        var declaration = new VariableDefinition { id = "mood", initialValue = NovelValue.Enum("calm"), members = ["calm", "happy"] };
        variables.Add(draft, declaration);
        declaration.members[0] = "mutated";
        Check(draft.variables.Single(item => item.id == "mood").members[0] == "calm", "Variable declaration retained mutable aliases.");
        variables.Remove(draft, "mood");
        Check(draft.variables.All(item => item.id != "mood"), "Unused declaration removal failed.");
        variables.Add(draft, new() { id = "reasonOnly", initialValue = NovelValue.String("Reason") });
        variables.Add(draft, new() { id = "escapedOnly", initialValue = NovelValue.Boolean(false) });
        var choiceRecord = draft.graph.document.FindNode(new("choice"))!;
        var choice = codec.Read<ChoiceNode>(choiceRecord); choice.prompt = "Literal {{escapedOnly}}"; choice.options[0].disabledReason = "Requires {reasonOnly}";
        codec.Write(choiceRecord, choice);
        Check(variables.FindUsages(draft, "reasonOnly").SequenceEqual(new[] { "choice" }), "A disabled reason's sole variable reference was omitted.");
        bool reasonRejected = false;
        try { variables.Remove(draft, "reasonOnly"); } catch (InvalidOperationException) { reasonRejected = true; }
        Check(reasonRejected && draft.variables.Any(static item => item.id == "reasonOnly"), "Reference-aware removal broke a disabled option reason.");
        Check(variables.FindUsages(draft, "escapedOnly").Count == 0, "Escaped literal braces were incorrectly counted as a variable reference.");
        variables.Remove(draft, "escapedOnly");
        Check(compiler.Compile(draft.ToAsset()).success, "Removing a literal-only variable broke the shared compiler template grammar.");
    }

    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}

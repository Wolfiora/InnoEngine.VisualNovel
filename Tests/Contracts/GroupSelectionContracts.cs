using System;
using System.Collections.Generic;
using System.Linq;
using Inno.Core.Graphs;
using Inno.Editor.Graph;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.ScriptProjection;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Narrative.Execution;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class GroupSelectionContracts
{
    internal static void NativeMembershipRangesAndReconciliation(
        IStorySerialization native,
        StoryNodeDataCodec codec
    ) {
        var draft = FirstStoryTemplate.Create(codec); string[] order = ScriptProjectionBuilder.Build(draft).Select(static row => row.nodeId).ToArray();
        byte[] before = native.Serialize(draft); var selection = new GraphCanvasState();
        Check(StoryNodeGroupSelection.Choose(selection, order, "hello", "", false, false) == "hello", "Plain script selection lost its primary identity.");
        StoryNodeGroupSelection.Choose(selection, order, "sea", "hello", false, true);
        Check(selection.selectedNodes.Select(static id => id.value).ToHashSet().SetEquals(["hello", "sea"]), "Additive script selection replaced native graph membership.");
        Check(StoryNodeGroupSelection.Choose(selection, order, "hello", "hello", false, true) == "sea" && selection.selectedNodes.Single().value == "sea",
            "Toggling the primary member did not select another retained native member.");
        StoryNodeGroupSelection.Choose(selection, order, "forest", "hello", true, false);
        int start = Array.IndexOf(order, "hello"), end = Array.IndexOf(order, "forest");
        var range = order.Skip(Math.Min(start, end)).Take(Math.Abs(start - end) + 1).ToArray();
        Check(selection.selectedNodes.Select(static id => id.value).SequenceEqual(range), "Shift selection used source array positions instead of the actual finite script projection.");
        StoryNodeGroupSelection.Choose(selection, order, "entry", "choice", true, true);
        Check(range.All(id => selection.selectedNodes.Contains(new(id))) && selection.selectedNodes.Contains(new("entry")), "Additive ranges discarded preceding membership.");
        var selected = selection.selectedNodes.ToArray();
        Reject(() => StoryNodeGroupSelection.Choose(selection, ["hello", "hello"], "hello", "", false, false));
        Reject(() => StoryNodeGroupSelection.Choose(selection, order, "missing", "", false, false));
        Check(selection.selectedNodes.SequenceEqual(selected) && native.Serialize(draft).SequenceEqual(before), "An invalid selection or normal range action mutated source graph data.");
        Check(StoryNodeGroupSelection.Reconcile(selection, draft.graph.document, "hello") == "hello" && selection.selectedNodes.Count > 1,
            "Switching the primary within a group collapsed the native selection.");
        draft.graph.document.RemoveNode(new("hello"));
        string primary = StoryNodeGroupSelection.Reconcile(selection, draft.graph.document, "hello");
        Check(!selection.selectedNodes.Contains(new("hello")) && primary.Length > 0 && selection.selectedNodes.Contains(new(primary)), "Deleting a primary left a stale native group member.");
        selection.SelectNodes([new("sea")]); StoryNodeGroupSelection.Reconcile(selection, draft.graph.document, "entry");
        Check(selection.selectedNodes.Single().value == "entry", "An explicit external primary did not replace unrelated local membership.");
        var independent = new GraphCanvasState(); independent.SelectNodes([new("forest")]);
        StoryNodeGroupSelection.Choose(selection, order, "entry", "", false, true);
        Check(selection.selectedNodes.Count == 0 && independent.selectedNodes.Single().value == "forest", "Clearing one workspace leaked into another owner.");
    }

    internal static void ReviewedGroupPasteAndMissingTargets(
        IStorySerialization native,
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var source = FirstStoryTemplate.Create(codec); var clipboard = new StoryClipboardService(native, codec); var edits = new StoryEditService(codec);
        byte[] before = native.Serialize(source), bytes = clipboard.Capture(source, ["hello", "choice", "sea"]);
        var review = clipboard.Review(bytes);
        Check(review.chapterId == source.chapterId && review.nodeIds.SequenceEqual(["hello", "choice", "sea"]) && review.externalTargets.SequenceEqual(["end", "forest"]),
            "Clipboard review omitted group boundaries or mixed internal targets with external ones.");
        bool readOnly = false; try { ((IList<string>)review.nodeIds)[0] = "changed"; } catch (NotSupportedException) { readOnly = true; }
        Check(readOnly, "A clipboard review exposed mutable captured identities.");
        var target = FirstStoryTemplate.Create(codec);
        string[] pasted = clipboard.Paste(target, "opening", bytes, new(50, 100), new Dictionary<string, string> { ["end"] = "end", ["forest"] = "hello" });
        Check(pasted.Length == 3 && pasted.All(id => !review.nodeIds.Contains(id) && target.graph.document.FindNode(new(id)) is not null), "Group paste reused source identities or lost internal members.");
        var nodes = pasted.Select(id => target.graph.document.FindNode(new(id))!).ToArray();
        var entry = nodes.Single(node => node.definitionId == StoryNodeIds.dialogue && codec.Read<DialogueNode>(node).text.Contains("你好", StringComparison.Ordinal));
        var choice = nodes.Single(static node => node.definitionId == StoryNodeIds.choice); var choiceData = codec.Read<ChoiceNode>(choice);
        Check(choiceData.options.All(static option => option.id is not ("sea" or "forest")), "Group paste did not remap dynamic choice ports.");
        edits.Connect(target, new(new("entry"), new("next")), entry.id);
        var compiled = compiler.Compile(target.ToAsset()); Check(compiled.success, string.Join("; ", compiled.diagnostics.Select(static issue => issue.message)));
        using (var machine = new StoryMachine(compiled.program!))
        {
            machine.Start(); machine.Advance(); machine.Advance(); machine.Choose(choiceData.options.Single(static option => option.text.Contains("林", StringComparison.Ordinal)).id);
            Check(machine.sourceNodeId == "hello", "The actual pasted branch did not follow its explicitly reviewed external destination.");
        }
        target = FirstStoryTemplate.Create(codec); pasted = clipboard.Paste(target, "opening", bytes, new(0, 0));
        Check(target.graph.document.edges.Where(edge => pasted.Contains(edge.output.nodeId.value)).Count(static edge => edge.input.nodeId.value.StartsWith("missing/copied/", StringComparison.Ordinal)) == 2,
            "Cross-chapter paste guessed matching local IDs instead of retaining unresolved external targets as Missing.");
        Check(!compiler.Compile(target.ToAsset()).success, "Missing copied targets were silently executable.");
        Check(native.Serialize(source).SequenceEqual(before) && clipboard.Review(bytes).nodeIds.SequenceEqual(review.nodeIds), "Review or target editing rewrote the source graph or native clipboard buffer.");
    }
    private static void Reject(Action action)
    { try { action(); } catch (ArgumentException) { return; } catch (InvalidOperationException) { return; } throw new InvalidOperationException("An invalid group selection was accepted."); }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}

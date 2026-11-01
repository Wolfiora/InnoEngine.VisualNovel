using System;
using System.Linq;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Narrative.Execution;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class StoryNotesContracts
{
    internal static void NativeCopySearchAndSemantics(
        IStorySerialization native,
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var draft = FirstStoryTemplate.Create(codec); var notes = new StoryNotesEditService(native); var search = new StorySearchService(codec, notes);
        var original = compiler.Compile(draft.ToAsset()).program!;
        notes.Set(draft, "hello", new() { note = "作者备注\n镜头应保持温暖。", tags = ["序章", "VoiceReview"], bookmarked = true });
        var restored = native.Deserialize<StoryDraftState>(native.Serialize(draft));
        var annotation = notes.Read(restored.graph.document.FindNode(new("hello"))!);
        Check(annotation.bookmarked && annotation.tags.SequenceEqual(new[] { "序章", "VoiceReview" }) && annotation.note.Contains('\n'), "Native graph roundtrip lost author annotations.");
        Check(search.Find(restored, "voicereview").Single().nodeId == "hello" && search.Find(restored, "", true).Count == 1
            && search.Find(restored, "去看海").Single().nodeId == "choice", "Search ignored tags, bookmarks or nested choice text.");
        var copy = new StoryClipboardService(native, codec); string[] ids = copy.Paste(restored, "opening", copy.Capture(restored, ["hello"]), new(100, 100));
        Check(ids.Single() != "hello" && notes.Read(restored.graph.document.FindNode(new(ids[0]))!).bookmarked, "Graph clipboard discarded annotations or reused node identity.");
        var annotated = compiler.Compile(draft.ToAsset()).program!;
        using var first = new StoryMachine(original); using var second = new StoryMachine(annotated); first.Start(); second.Start();
        first.Advance(); second.Advance(); first.Advance(); second.Advance(); first.Choose("sea"); second.Choose("sea");
        Check(first.nodeId == second.nodeId && first.dialogueText == second.dialogueText, "Annotations changed control-flow or visible story behavior.");
        var missing = new Inno.Core.Graphs.GraphNodeRecord(new("missing"), "missing.extension"); restored.graph.document.AddNode(missing);
        notes.Set(restored, "missing", new() { note = "等待恢复扩展", bookmarked = true });
        Check(search.Find(restored, "恢复扩展", true).Single().nodeId == "missing", "Missing node annotations depended on interpreting opaque command data.");
        try { notes.Set(restored, "hello", new() { tags = ["same", "SAME"] }); throw new ArgumentException("Duplicate tags accepted."); } catch (InvalidOperationException) { }
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Inno.Assets;
using Inno.Assets.Pipeline;
using Inno.Core.Graphs;
using Inno.Editor.Interactions;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Reading;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class GameSearchContracts
{
    internal static void BoundedSnapshotsAndCancellation(
        IStorySerialization native,
        StoryNodeDataCodec codec
    ) {
        var notes = new StoryNotesEditService(native); var search = new StorySearchService(codec, notes);
        var one = FirstStoryTemplate.Create(codec); var two = FirstStoryTemplate.Create(codec);
        one.title = "Chapter one"; two.title = "Chapter two";
        SetText(one, "needle saved"); SetText(two, "needle unsaved");
        one.graph.document.AddNode(new(new("opaque"), "missing.extension"));
        notes.Set(one, "opaque", new() { note = "needle annotation", bookmarked = true });
        AssetPath first = AssetPath.Project("One.ivnstory"), second = AssetPath.Project("Two.ivnstory"), failed = AssetPath.Project("Missing.ivnstory");
        var drafts = new Dictionary<AssetPath, StoryDraftState> { [first] = one, [second] = two };
        int reads = 0;
        using (var job = new GameStorySearch(new[] { first, failed, second }, search, Inspect, "needle"))
        {
            job.Step(1); Check(job.processedNodes == 1 && reads == 1, "A single search step scanned multiple chapters or ignored its node budget.");
            for (int count = 0; !job.completed && count < 100; count++) job.Step(2);
            Check(job.completed && !job.cancelled && job.results.Count == 3 && job.issues.Count == 1 && job.completedChapters == 3,
                "A broken chapter discarded other matches or prevented finite completion.");
            Check(job.results.Count(static result => result.match.nodeId == "hello") == 2 && job.results.Any(result => result.path == second && result.unsaved)
                && job.results.Any(static result => result.match.nodeId == "opaque" && result.match.bookmarked),
                "Game search confused same-named nodes in different chapters or discarded opaque author notes.");
        }
        int previousReads = reads;
        using (var cancelled = new GameStorySearch(new[] { first, second }, search, Inspect, "never matches"))
        {
            cancelled.Step(1); long nodes = cancelled.processedNodes; cancelled.Cancel(); cancelled.Step(1);
            Check(cancelled.cancelled && cancelled.completed && cancelled.processedNodes == nodes && reads == previousReads + 1,
                "Cancelled search continued scanning nonmatches or opened another source.");
        }
        using (var bookmarks = new GameStorySearch(new[] { first, second }, search, Inspect, "", true))
        {
            for (int count = 0; !bookmarks.completed && count < 100; count++) bookmarks.Step();
            Check(bookmarks.results.Count == 1 && bookmarks.results[0].match.nodeId == "opaque", "Whole-game bookmarks differ from the existing chapter search rules.");
        }

        StoryDocumentSnapshot Inspect(AssetPath path)
        {
            reads++;
            if (!drafts.TryGetValue(path, out var draft)) throw new IOException("Missing native source");
            return new(path, native.Deserialize<StoryDraftState>(native.Serialize(draft)), Guid.Empty, path == second, path.ToString());
        }
        void SetText(StoryDraftState draft, string text)
        { var node = draft.graph.document.FindNode(new("hello"))!; var line = codec.Read<DialogueNode>(node); line.text = text; codec.Write(node, line); }
    }

    internal static void NativeInspection(
        StoryDocumentsModule documents,
        EditorInteractions interactions,
        EditorDocumentContext openDocument
    ) {
        var nativePath = AssetPath.Project("Game/SearchOnly.ivnstory");
        var saved = FirstStoryTemplate.Create(documents.codec); saved.title = "Unopened chapter";
        var savedLine = documents.codec.Read<DialogueNode>(saved.graph.document.FindNode(new("hello"))!); savedLine.text = "search needle saved";
        documents.codec.Write(saved.graph.document.FindNode(new("hello"))!, savedLine);
        Check(EditorAssets.Save(nativePath, saved.ToAsset()), "The unopened search fixture did not save through native assets.");
        int count = interactions.documents.documents.Count; Guid active = documents.activeDocumentId;
        object? selection = interactions.selection.selectedTarget;
        documents.Edit(openDocument.documentId, "Keep search draft unsaved", source =>
        {
            var node = source.graph.document.FindNode(new("hello"))!; var line = documents.codec.Read<DialogueNode>(node); line.text = "search needle dirty"; documents.codec.Write(node, line);
        });
        var openPath = AssetPath.Parse(openDocument.assetPath);
        var open = documents.Inspect(openPath); var unopened = documents.Inspect(nativePath);
        Check(open.unsaved && open.documentId == openDocument.documentId && unopened.documentId == Guid.Empty && !unopened.unsaved,
            "The native source inspector ignored a dirty document or opened an unrelated chapter.");
        using var job = new GameStorySearch(new[] { openPath, nativePath }, documents.search, documents.Inspect, "search needle");
        for (int index = 0; !job.completed && index < 100; index++) job.Step(2);
        Check(job.results.Count == 2 && job.issues.Count == 0 && documents.activeDocumentId == active
            && interactions.documents.documents.Count == count && Equals(selection, interactions.selection.selectedTarget) && openDocument.isDirty,
            "Game search changed selection, opened documents or saved an author's unsaved work.");
        var nodeInCopy = open.draft.graph.document.FindNode(new("hello"))!;
        var edited = documents.codec.Read<DialogueNode>(nodeInCopy); edited.text = "mutated snapshot"; documents.codec.Write(nodeInCopy, edited);
        Check(documents.codec.Read<DialogueNode>(documents.Read(openDocument.documentId).graph.document.FindNode(new("hello"))!).text == "search needle dirty",
            "Search snapshots retained mutable aliases into the native document owner.");
        string fingerprint = job.results.Single(result => result.path == openPath).fingerprint;
        documents.Edit(openDocument.documentId, "Change search result after inspection", source =>
        {
            var node = source.graph.document.FindNode(new("hello"))!; var line = documents.codec.Read<DialogueNode>(node); line.text = "later edit"; documents.codec.Write(node, line);
        });
        Check(documents.Inspect(openPath).fingerprint != fingerprint, "Stale search results could not distinguish later native document edits.");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

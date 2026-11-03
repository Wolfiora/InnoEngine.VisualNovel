using System;
using System.IO;
using System.Linq;
using Inno.Assets.Pipeline;
using Inno.Core.Identity;
using Inno.Core.Input;
using Inno.Core.Logging;
using Inno.Core.Serialization;
using Inno.Editor.Core;
using Inno.Editor.Interactions;
using Inno.Extensibility.Types;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.Preview;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Model.Performance;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class PreviewDraftContracts
{
    internal static void Run(
        string root,
        TypeCatalog types,
        SerializationRegistry native,
        IdentityAllocator identities,
        AssetPipeline assets,
        LogRouter logs,
        NovelProjectAsset game
    ) {
        using var runtime = new EditorInteractionRuntime(new EditorContext(root, new EditorKeyboardPolicy(KeyModifier.Control, "Super")),
            types, logs, [native, types, identities, assets]);
        runtime.Start();
        if (!runtime.interactions.TryGetModule<StoryDocumentsModule>(out var stories) || stories is null
            || !runtime.interactions.TryGetModule<ResourceDocumentsModule>(out var documents) || documents is null)
            throw new InvalidOperationException("Shared native preview document owners did not activate.");
        var gameDoc = documents.Open(game.assetPath);
        documents.Edit<NovelProjectAsset>(gameDoc.documentId, "Unsaved preview game title", value => value.title = "Unsaved context game");
        var actor = game.characters[0]; string actorName = actor.displayName;
        var actorDoc = documents.Open(actor.assetPath);
        documents.Edit<CharacterAsset>(actorDoc.documentId, "Unsaved preview actor", value => value.displayName = "Unsaved actor");
        var stage = game.stages[0]; double stageX = stage.layers[0].transform.x;
        var stageDoc = documents.Open(stage.assetPath);
        documents.Edit<StageAsset>(stageDoc.documentId, "Unsaved preview scenery", value => value.layers[0].transform.x = 111);
        var theme = game.themes.First(value => value.resourceId != game.defaultTheme);
        var page = theme.screens.Single(value => value.role == "reading").screen!;
        uint background = page.root.style.background;
        var pageDoc = documents.Open(page.assetPath);
        documents.Edit<ScreenAsset>(pageDoc.documentId, "Unsaved nested theme page", value => value.root.style.background = 0x123456FF);
        var themeDoc = documents.Open(theme.assetPath);
        documents.Edit<ThemeAsset>(themeDoc.documentId, "Unsaved theme title", value => value.title = "Unsaved nested theme");
        var other = game.chapters.First(value => value.chapterId != game.startChapter);
        string originalProgram = other.program!.fingerprint;
        var otherDoc = stories!.Open(other.assetPath);
        var line = stories.Read(otherDoc.documentId).graph.document.nodes.First(value => value.definitionId == StoryNodeIds.dialogue);
        stories.Edit(otherDoc.documentId, "Unsaved other chapter", value =>
        {
            var text = stories.codec.Read<DialogueNode>(value.graph.document.FindNode(line.id)!); text.text = "Unsaved referenced chapter";
            stories.edits.SetSettings(value, line.id, text);
        });
        var firstChapter = game.chapters.First(value => value.chapterId == game.startChapter);
        string firstNode = firstChapter.program!.instructions.First(static instruction => instruction.sourceNodeId.Length > 0).sourceNodeId;
        int beforeNavigation = runtime.interactions.documents.documents.Count;
        object? selectionBeforeNavigation = runtime.interactions.selection.selectedTarget;
        var sourceTarget = PreviewSourceNavigator.Resolve(game, stories, firstChapter.chapterId, firstNode);
        Check(sourceTarget.path.Equals(firstChapter.assetPath) && stories.activeDocumentId == otherDoc.documentId
            && runtime.interactions.documents.documents.Count == beforeNavigation && Equals(runtime.interactions.selection.selectedTarget, selectionBeforeNavigation),
            "Resolving a cross-chapter debug source opened a document or changed native selection.");
        var firstDoc = stories.Open(firstChapter.assetPath);
        stories.Edit(otherDoc.documentId, "Rename dirty debug source", value => value.chapterId = "debug-renamed-chapter");
        var dirtyTarget = PreviewSourceNavigator.Resolve(game, stories, "debug-renamed-chapter", line.id.value);
        Check(dirtyTarget.unsaved && dirtyTarget.documentId == otherDoc.documentId && dirtyTarget.path.Equals(other.assetPath)
            && stories.activeDocumentId == firstDoc.documentId, "Debug navigation preferred stale imported chapter metadata over an existing dirty source.");
        RejectSource(() => PreviewSourceNavigator.Resolve(game, stories, other.chapterId, line.id.value));
        RejectSource(() => PreviewSourceNavigator.Resolve(game, stories, firstChapter.chapterId, "deleted-debug-node"));
        RejectSource(() => PreviewSourceNavigator.Resolve(new NovelProjectAsset { chapters = [firstChapter, firstChapter] }, stories, firstChapter.chapterId, firstNode));
        Check(runtime.interactions.history.Undo().succeeded, "Could not restore the renamed debug source.");
        stories.activeDocumentId = otherDoc.documentId;
        int count = runtime.interactions.documents.documents.Count;
        runtime.interactions.SetSelection("preview selection sentinel");
        Guid selectedStory = stories.activeDocumentId, selectedResource = documents.activeDocument!.documentId;
        byte[] source = File.ReadAllBytes(Path.Combine(root, "Assets", stage.assetPath.localPath));
        var snapshot = PreviewDraftProjectBuilder.Build(game, stories, documents);
        Check(snapshot.unsavedCount == 6 && snapshot.project.title == "Unsaved context game"
            && snapshot.project.characters[0].displayName == "Unsaved actor" && snapshot.project.stages[0].layers[0].transform.x == 111,
            "Preview omitted existing dirty game, character or stage documents.");
        var resolved = snapshot.project.themes.Single(value => value.resourceId == theme.resourceId);
        Check(resolved.title == "Unsaved nested theme" && resolved.screens.Single(value => value.role == "reading").screen!.root.style.background == 0x123456FF,
            "Theme-only page dependencies did not receive their shared dirty document.");
        Check(snapshot.project.chapters.Single(value => value.chapterId == other.chapterId).program!.instructions.Any(value => value.dialogue?.text == "Unsaved referenced chapter")
            && other.program.fingerprint == originalProgram, "Dirty referenced chapter was not compiled independently of the canonical runtime program.");
        Check(game.title != snapshot.project.title && actor.displayName == actorName && stage.layers[0].transform.x == stageX && page.root.style.background == background
            && source.AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(root, "Assets", stage.assetPath.localPath)))
            && runtime.interactions.documents.documents.Count == count && Equals(runtime.interactions.selection.selectedTarget, "preview selection sentinel")
            && stories.activeDocumentId == selectedStory && documents.activeDocument!.documentId == selectedResource,
            "Preview projection saved content, mutated canonical resources, opened documents or changed shared selection.");
        var repeat = PreviewDraftProjectBuilder.Build(game, stories, documents);
        Check(snapshot.project.buildFingerprint == repeat.project.buildFingerprint, "Unchanged native draft inputs produced different preview fingerprints.");
        using (var cancelled = PreviewDraftProjectBuilder.Begin(game, stories, documents))
        {
            Check(!cancelled.Advance(1, 1000) && cancelled.capturedSources == 0 && cancelled.result is null,
                "Beginning bounded preparation eagerly scanned the native resource closure.");
            string phase = cancelled.progress;
            Check(phase.StartsWith("Capturing resource · ", StringComparison.Ordinal), "Preparation did not report its English phase and authored source path.");
            Check(!cancelled.Advance(1, 1000) && cancelled.capturedSources == 1 && cancelled.result is null,
                "One bounded preparation step consumed multiple source captures or published a partial project.");
            cancelled.Cancel();
            Check(cancelled.cancelled && !cancelled.running && cancelled.Advance() && cancelled.result is null && cancelled.error.Length == 0,
                "Cancellation resumed the native iterator, exposed partial state or became a preparation failure.");
        }
        using (var incremental = PreviewDraftProjectBuilder.Begin(game, stories, documents))
        {
            int updates = 0;
            while (!incremental.Advance(2, 1000))
            {
                Check(incremental.result is null && ++updates < 10000, "Preparation published an incomplete project or did not finish its bounded closure.");
            }
            Check(updates > 2 && incremental.result?.project.buildFingerprint == snapshot.project.buildFingerprint
                && incremental.result.unsavedCount == 6 && !incremental.cancelled && incremental.error.Length == 0,
                "Bounded preparation differed from the synchronous production builder or lost unsaved provenance.");
        }
        using (var stale = PreviewDraftProjectBuilder.Begin(game, stories, documents))
        {
            stale.Advance(1, 1000); stale.Advance(1, 1000);
            documents.Edit<NovelProjectAsset>(gameDoc.documentId, "Change already captured preparation root", value => value.title += " later");
            while (!stale.Advance(16, 1000)) { }
            Check(stale.result is null && stale.failure is Inno.VisualNovel.Authoring.Diagnostics.AuthoringValidationException
                && stale.error.Contains(game.assetPath.ToString(), StringComparison.Ordinal)
                && !stale.cancelled && !stale.running && snapshot.project.title == "Unsaved context game",
                "A document edited after native capture became a mixed-time candidate or replaced an earlier valid snapshot.");
            Check(runtime.interactions.history.Undo().succeeded, "Could not restore the preparation source after the stale-candidate test.");
        }
        Check(runtime.interactions.documents.documents.Count == count && Equals(runtime.interactions.selection.selectedTarget, "preview selection sentinel")
            && source.AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(root, "Assets", stage.assetPath.localPath))),
            "Incremental preparation changed shared selection, opened documents or saved source content.");
        var deepCase = new NovelPreviewCaseAsset { mode = PreviewRunMode.Local, nodeId = line.id.value, stage = new() { cameraX = 37 }, initialValues = [new() { id = "example", value = NovelValue.String("Frozen") }] };
        var frozenCase = EditorAssets.DecodeNative<NovelPreviewCaseAsset>(EditorAssets.EncodeNative(deepCase));
        deepCase.stage.cameraX = 99; deepCase.initialValues[0].value = NovelValue.String("Changed");
        Check(frozenCase.stage.cameraX == 37 && frozenCase.initialValues[0].value.text == "Frozen", "Native queued preview case capture retained mutable stage or variable input state.");
        var freshness = new PreviewFreshnessTracker(snapshot, stories, documents);
        Check(freshness.Probe(1) && freshness.CheckAll() && !freshness.stale,
            "Unchanged shared dirty documents invalidated their own captured preview.");
        var previewCase = documents.Create(new NovelPreviewCaseAsset());
        documents.Edit<NovelPreviewCaseAsset>(previewCase.documentId, "Unrelated native preview case", value => value.title = "Unrelated");
        Check(freshness.CheckAll(), "A resource outside the captured closure invalidated an unrelated preview.");
        var caseFreshness = new PreviewFreshnessTracker(snapshot, stories, documents, caseDocument: previewCase.documentId);
        var beforeSave = documents.GetStamp(previewCase.documentId)!;
        Check(runtime.interactions.documents.Save(previewCase.documentId)
            && beforeSave.revision == documents.GetStamp(previewCase.documentId)!.revision && !caseFreshness.Probe(1),
            "Saving an originating case without changing its edit revision escaped freshness checks.");
        var clipDoc = documents.Open(game.performances[0].assetPath);
        Check(freshness.CheckAll() && runtime.interactions.documents.Close(clipDoc.documentId, EditorDocumentCloseMode.Save)
            && freshness.CheckAll(), "Opening or closing an unchanged captured resource invalidated its running preview.");
        clipDoc = documents.Open(game.performances[0].assetPath);
        var clipFreshness = new PreviewFreshnessTracker(snapshot, stories, documents);
        documents.Edit<PerformanceClipAsset>(clipDoc.documentId, "Change captured clip", value => value.displayName = "Changed clip");
        Check(!clipFreshness.CheckAll() && runtime.interactions.history.Undo().succeeded && clipFreshness.stale
            && !clipFreshness.CheckAll(), "A changed input or subsequent undo silently certified a previously stale route.");
        Check(runtime.interactions.documents.Close(clipDoc.documentId, EditorDocumentCloseMode.Discard)
            && new PreviewFreshnessTracker(snapshot, stories, documents).CheckAll(), "Discarding an unrelated later edit poisoned a newly started preview.");
        var explicitCase = new PreviewFreshnessTracker(snapshot, stories, documents, caseDocument: previewCase.documentId);
        Check(runtime.interactions.documents.Close(previewCase.documentId, EditorDocumentCloseMode.Discard) && !explicitCase.CheckAll(),
            "Closing the originating case allowed recording into a replaced or absent document.");
        documents.Edit<StageAsset>(stageDoc.documentId, "Change preview scenery", value => value.layers[0].transform.x = 222);
        for (int index = 0; index < snapshot.sources.Count && !freshness.stale; index++) freshness.Probe(1);
        Check(freshness.stale && freshness.reason.Contains(stage.assetPath.ToString(), StringComparison.Ordinal),
            "Bounded freshness polling did not eventually identify the edited native source.");
        var changed = PreviewDraftProjectBuilder.Build(game, stories, documents);
        var activeFreshness = new PreviewFreshnessTracker(changed, stories, documents, otherDoc.documentId);
        stories.Edit(otherDoc.documentId, "Change explicitly previewed chapter", value => value.title += " changed");
        Check(!activeFreshness.Probe(1) && runtime.interactions.history.Undo().succeeded,
            "The explicitly previewed chapter did not invalidate immediately before the bounded closure scan.");
        Check(changed.project.buildFingerprint != snapshot.project.buildFingerprint && snapshot.project.stages[0].layers[0].transform.x == 111,
            "A later draft mutation changed an existing snapshot or escaped its strict fingerprint.");
        var library = game.chapters[0].variableTable ?? throw new InvalidOperationException("The project wizard omitted its shared variable library.");
        Check(game.chapters.All(chapter => chapter.variableTable?.identity.persistentId == library.identity.persistentId), "Wizard chapters did not reference one native declaration authority.");
        var shared = library.variables.First(static item => item.initialValue.kind == NovelValueKind.String);
        var originalValue = shared.initialValue.Copy();
        byte[] tableBytes = File.ReadAllBytes(Path.Combine(root, "Assets", library.assetPath.localPath));
        var libraryDoc = documents.Open(library.assetPath);
        documents.Edit<VariableTableAsset>(libraryDoc.documentId, "Unsaved shared default", table => table.variables.Single(item => item.id == shared.id).initialValue = NovelValue.String("Unsaved library default"));
        var librarySnapshot = PreviewDraftProjectBuilder.Build(game, stories, documents, library);
        Check(librarySnapshot.unsavedCount == 7 && librarySnapshot.project.chapters.All(chapter => chapter.program!.variables.Single(item => item.id == shared.id).initialValue.text == "Unsaved library default")
            && library.variables.Single(item => item.id == shared.id).initialValue.Equals(originalValue)
            && tableBytes.AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(root, "Assets", library.assetPath.localPath))),
            "Shared draft projection omitted dependent chapters, mutated canonical defaults or wrote the library source.");
        var active = stories.Read(otherDoc.documentId);
        var activeContent = PreviewContentBuilder.Build(librarySnapshot.project, active, stories.compiler, new() { mode = PreviewRunMode.Complete }, librarySnapshot.GetVariableTable(active.variableTableId));
        Check(activeContent.project.chapters.Single(chapter => chapter.chapterId == active.chapterId).program!.variables.Single(item => item.id == shared.id).initialValue.text == "Unsaved library default",
            "Recompiling the active draft replaced its prepared library with canonical saved defaults.");
        documents.Edit<VariableTableAsset>(libraryDoc.documentId, "Later shared default", table => table.variables.Single(item => item.id == shared.id).initialValue = NovelValue.String("Later default"));
        Check(librarySnapshot.GetVariableTable(library.identity.persistentId)!.variables.Single(item => item.id == shared.id).initialValue.text == "Unsaved library default",
            "A later library edit changed captured preview declaration values.");
        documents.Edit<VariableTableAsset>(libraryDoc.documentId, "Invalid shared lifetime", table => table.variables[0].scope = VariableScope.Local);
        bool libraryRejected = false;
        try { PreviewDraftProjectBuilder.Build(game, stories, documents); } catch (InvalidOperationException error) { libraryRejected = error.Message.Contains("VN_VARIABLE_TABLE", StringComparison.Ordinal); }
        Check(libraryRejected && other.program.fingerprint == originalProgram, "An invalid dirty library replaced canonical dependent programs or lost its compile diagnostic.");
        runtime.interactions.history.Undo();
        stories.Edit(otherDoc.documentId, "Invalid referenced chapter", value =>
        {
            var text = stories.codec.Read<DialogueNode>(value.graph.document.FindNode(line.id)!); text.charactersPerSecond = -1;
            stories.edits.SetSettings(value, line.id, text);
        });
        bool rejected = false;
        try { PreviewDraftProjectBuilder.Build(game, stories, documents); }
        catch (InvalidOperationException error) { rejected = error.Message.Contains(other.chapterId, StringComparison.Ordinal) && error.Message.Contains(line.id.value, StringComparison.Ordinal); }
        Check(rejected && other.program.fingerprint == originalProgram && snapshot.project.stages[0].layers[0].transform.x == 111,
            "Invalid referenced draft lost chapter/node diagnostics or replaced previously prepared content.");
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
    private static void RejectSource(Action action)
    {
        try { action(); } catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("A stale, missing or ambiguous debugger source was guessed.");
    }
}

using System;
using System.IO;
using System.Linq;
using Inno.Assets;
using Inno.Assets.Pipeline;
using Inno.Core.Diagnostics;
using Inno.Core.Identity;
using Inno.Core.Input;
using Inno.Core.Logging;
using Inno.Core.Serialization;
using Inno.Editor.Core;
using Inno.Editor.Interactions;
using Inno.Extensibility.Modules;
using Inno.Extensibility.Types;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Authoring.UiEditing;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class HistoryContracts
{
    internal static void Run(
        string projectRoot,
        ModuleHost modules,
        TypeCatalog types,
        SerializationRegistry serialization,
        IdentityAllocator identities
    ) {
        string root = Path.Combine(projectRoot, "artifacts", "AcceptanceProjects", "History-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Assets"));
        using var logs = new LogRouter();
        using var assets = new AssetPipeline(modules, types, serialization, identities, new DiagnosticHub(), logs,
            AssetPipelineOptions.Create(Path.Combine(root, "Assets"), Path.Combine(root, "Library")) with { enableFileSystemWatcher = false });
        using IDisposable assetScope = AssetExecutionContext.EnterScope(assets);
        using var runtime = new EditorInteractionRuntime(new EditorContext(root, new EditorKeyboardPolicy(KeyModifier.Control, "Super")),
            types, logs, [serialization, types, identities, assets]);
        runtime.Start();
        if (!runtime.interactions.TryGetModule<StoryDocumentsModule>(out var documents) || documents is null)
            throw new InvalidOperationException("The actual editor catalog did not activate the story document module.");
        var context = documents.CreateFirstStory();
        StoryNodeSelection.Select(runtime.interactions, context.documentId, "hello");
        if (!StoryNodeSelection.TryRead(runtime.interactions, context.documentId, out string selected) || selected != "hello"
            || StoryNodeSelection.TryRead(runtime.interactions, Guid.NewGuid(), out _)) throw new InvalidOperationException("Native node selection lost its document qualification.");
        runtime.interactions.SetSelection("unrelated-resource");
        StoryNodeSelection.Select(runtime.interactions, context.documentId, string.Empty);
        if (!Equals(runtime.interactions.selection.selectedTarget, "unrelated-resource")) throw new InvalidOperationException("Clearing node selection erased an unrelated resource selection.");
        string before = Line();
        documents.Edit(context.documentId, "Change dialogue", state =>
        {
            var node = state.graph.document.FindNode(new("hello"))!;
            DialogueNode line = documents.codec.Read<DialogueNode>(node);
            line.text = "History verified";
            documents.codec.Write(node, line);
        });
        if (!context.isDirty || Line() != "History verified") throw new InvalidOperationException("Edit did not publish shared draft state.");
        if (!runtime.interactions.history.Undo().succeeded || Line() != before || context.isDirty)
            throw new InvalidOperationException("Engine Undo did not restore the saved draft.");
        if (!runtime.interactions.history.Redo().succeeded || Line() != "History verified")
            throw new InvalidOperationException("Engine Redo did not restore the same document.");
        if (!runtime.interactions.documents.Save(context.documentId) || context.isDirty)
            throw new InvalidOperationException("Document save did not commit through the official asset API.");
        var quickDraft = documents.Read(context.documentId); int quickCount = quickDraft.graph.document.nodes.Count;
        Inno.VisualNovel.Authoring.StoryEditing.StoryInsertionResult? quickChoice = null;
        documents.Edit(context.documentId, "Quick choice with fragments", candidate => quickChoice = documents.quickInsert.Create(candidate, "opening", Model.Story.StoryNodeIds.choice,
            new(0, 0), documents.quickInsert.NextEdge(candidate, "hello")));
        if (quickChoice?.connected != true || documents.Read(context.documentId).graph.document.nodes.Count != quickCount + 3
            || !runtime.interactions.history.Undo().succeeded || documents.Read(context.documentId).graph.document.nodes.Count != quickCount
            || !runtime.interactions.history.Redo().succeeded || documents.Read(context.documentId).graph.document.FindNode(quickChoice.nodeId) is null
            || !runtime.interactions.documents.Save(context.documentId)) throw new InvalidOperationException("Quick branching creation bypassed one native History transaction or lost stable redo identity.");
        if (!runtime.interactions.history.Undo().succeeded || !runtime.interactions.documents.Save(context.documentId))
            throw new InvalidOperationException("Quick choice fixture cleanup did not restore the saved original continuation.");
        documents.Edit(context.documentId, "Bookmark annotated line", state => documents.notes.Set(state, "hello", new() { note = "配音待复核", tags = ["review"], bookmarked = true }));
        if (!documents.search.Find(documents.Read(context.documentId), "review", true).Any()
            || !runtime.interactions.history.Undo().succeeded || documents.notes.Read(documents.Read(context.documentId).graph.document.FindNode(new("hello"))!).bookmarked
            || !runtime.interactions.history.Redo().succeeded || !runtime.interactions.documents.Save(context.documentId))
            throw new InvalidOperationException("Author notes bypassed shared native History or failed to save.");
        if (!runtime.interactions.TryGetModule<ResourceDocumentsModule>(out var resources) || resources is null) throw new InvalidOperationException("The resource document owner did not activate.");
        var scenery = resources.Create(new Model.Stages.StageAsset { layers = [new() { id = "native-surface" }] });
        resources.Edit<Model.Stages.StageAsset>(scenery.documentId, "Edit source crop and pivot", candidate =>
        { candidate.layers[0].surface.cropWidth = .5; candidate.layers[0].surface.blend = Model.Stages.StageBlendMode.Multiply; candidate.layers[0].transform.pivotX = 25; });
        if (!runtime.interactions.history.Undo().succeeded || resources.Read<Model.Stages.StageAsset>(scenery.documentId).layers[0].surface.cropWidth != 1
            || !runtime.interactions.history.Redo().succeeded || resources.Read<Model.Stages.StageAsset>(scenery.documentId).layers[0].transform.pivotX != 25
            || !runtime.interactions.documents.Save(scenery.documentId)) throw new InvalidOperationException("Stage composition fields bypassed native History or source saving.");
        var stageStart = Inno.VisualNovel.Authoring.StageEditing.StageCanvasEditService.Capture(resources.Read<Model.Stages.StageAsset>(scenery.documentId), "native-surface");
        using (var move = new EditorEditGesture(runtime.interactions.history, "Native stage drag"))
        {
            resources.Edit<Model.Stages.StageAsset>(scenery.documentId, "Native stage drag", candidate => Inno.VisualNovel.Authoring.StageEditing.StageCanvasEditService.Move(candidate, stageStart, 8, 16), move.mergeKey);
            resources.Edit<Model.Stages.StageAsset>(scenery.documentId, "Native stage drag", candidate => Inno.VisualNovel.Authoring.StageEditing.StageCanvasEditService.Move(candidate, stageStart, 24, 32), move.mergeKey);
            move.Commit();
        }
        if (!runtime.interactions.history.Undo().succeeded || resources.Read<Model.Stages.StageAsset>(scenery.documentId).layers[0].transform.x != stageStart.x
            || !runtime.interactions.history.Redo().succeeded || resources.Read<Model.Stages.StageAsset>(scenery.documentId).layers[0].transform.x != stageStart.x + 24)
            throw new InvalidOperationException("Stage canvas samples did not merge into one native History operation.");
        var resizeStart = Inno.VisualNovel.Authoring.StageEditing.StageCanvasEditService.Capture(resources.Read<Model.Stages.StageAsset>(scenery.documentId), "native-surface");
        using (var resize = new EditorEditGesture(runtime.interactions.history, "Cancel native stage resize"))
        {
            resources.Edit<Model.Stages.StageAsset>(scenery.documentId, "Cancel native stage resize", candidate => Inno.VisualNovel.Authoring.StageEditing.StageCanvasEditService.Scale(candidate, resizeStart, 2), resize.mergeKey);
            if (!resize.Cancel().succeeded || resources.Read<Model.Stages.StageAsset>(scenery.documentId).layers[0].transform.scaleX != 1)
                throw new InvalidOperationException("Cancelling a stage canvas resize left a partial authored transform.");
        }
        if (!runtime.interactions.documents.Save(scenery.documentId)) throw new InvalidOperationException("Stage canvas edits did not save through native resource ownership.");
        var batchStage = resources.Create(new Model.Stages.StageAsset { layers = [new() { id = "one", transform = new() { scaleX = -1 } }, new() { id = "two", transform = new() { x = 100 } }] });
        resources.Edit<Model.Stages.StageAsset>(batchStage.documentId, "Native stage batch", candidate => Inno.VisualNovel.Authoring.StageEditing.StageLayerBatchEditService.Apply(candidate, ["one", "two"],
            new() { offset = new(8, 16), scale = 2, opacity = .5 }));
        if (!runtime.interactions.history.Undo().succeeded || resources.Read<Model.Stages.StageAsset>(batchStage.documentId).layers[0].transform.x != 0
            || resources.Read<Model.Stages.StageAsset>(batchStage.documentId).layers[1].transform.x != 100 || !runtime.interactions.history.Redo().succeeded
            || resources.Read<Model.Stages.StageAsset>(batchStage.documentId).layers[0].transform.scaleX != -2
            || !runtime.interactions.documents.Save(batchStage.documentId)) throw new InvalidOperationException("A stage batch bypassed one native History transaction or lost mirror signs on redo/save.");
        File.WriteAllBytes(Path.Combine(root, "Assets", "ActorPixel.png"), Inno.VisualNovel.UI.Imaging.UiPngImage.Encode(new(1, 1, new byte[] { 255, 255, 255, 255 })));
        var actorImage = Inno.Assets.Assets.Load<Inno.Rendering.Assets.TextureAsset>(AssetPath.Project("ActorPixel.png"));
        var actor = resources.Create(Inno.VisualNovel.Authoring.CharacterEditing.CharacterEditService.FromImage(actorImage)); string alternatePose = "";
        resources.Edit<Model.Characters.CharacterAsset>(actor.documentId, "Duplicate and select pose", candidate =>
        {
            alternatePose = Inno.VisualNovel.Authoring.CharacterEditing.CharacterEditService.DuplicatePose(candidate, candidate.defaultPose);
            candidate.poses[1].name = "Alternate native pose"; candidate.defaultPose = alternatePose;
        });
        bool rejectedPose = false;
        try { resources.Edit<Model.Characters.CharacterAsset>(actor.documentId, "Reject implicit default replacement", candidate =>
            Inno.VisualNovel.Authoring.CharacterEditing.CharacterVariantEditService.RemovePose(candidate, candidate.defaultPose)); }
        catch (InvalidOperationException) { rejectedPose = true; }
        if (!rejectedPose || resources.Read<Model.Characters.CharacterAsset>(actor.documentId).poses.Length != 2
            || !runtime.interactions.history.Undo().succeeded || resources.Read<Model.Characters.CharacterAsset>(actor.documentId).poses.Length != 1
            || !runtime.interactions.history.Redo().succeeded || resources.Read<Model.Characters.CharacterAsset>(actor.documentId).defaultPose != alternatePose
            || !runtime.interactions.documents.Save(actor.documentId)) throw new InvalidOperationException("Character variant rejection, native History or source saving changed the explicit default contract.");
        Directory.CreateDirectory(Path.Combine(root, "Assets", "ActorBatch"));
        File.WriteAllBytes(Path.Combine(root, "Assets", "ActorBatch", "pose__Alternate native pose__body.png"),
            Inno.VisualNovel.UI.Imaging.UiPngImage.Encode(new(2, 1, new byte[] { 128, 192, 255, 255, 255, 192, 128, 255 })));
        using (var preparation = new Inno.VisualNovel.Authoring.CharacterEditing.ImageImport.NativeCharacterImagePreparation(root, "ActorBatch"))
        {
            while (!preparation.finished) preparation.Step();
            var batch = Inno.VisualNovel.Authoring.CharacterEditing.ImageImport.CharacterImageBatchService.Preview(resources.Read<Model.Characters.CharacterAsset>(actor.documentId), preparation.inputs);
            long beforeBatch = resources.GetRevision(actor.documentId);
            resources.Edit<Model.Characters.CharacterAsset>(actor.documentId, "Apply reviewed image batch", candidate =>
                Inno.VisualNovel.Authoring.CharacterEditing.ImageImport.CharacterImageBatchService.ApplyReady(candidate, batch));
            if (batch.readyCount != 1 || resources.GetRevision(actor.documentId) <= beforeBatch
                || resources.Read<Model.Characters.CharacterAsset>(actor.documentId).textures.Length != 2
                || !runtime.interactions.history.Undo().succeeded || resources.Read<Model.Characters.CharacterAsset>(actor.documentId).textures.Length != 1
                || !runtime.interactions.history.Redo().succeeded || resources.Read<Model.Characters.CharacterAsset>(actor.documentId).defaultPose != alternatePose
                || !runtime.interactions.documents.Save(actor.documentId)) throw new InvalidOperationException("Image batch bypassed native revision, atomic Undo/Redo, stable defaults or source saving.");
        }
        int fragments = documents.Read(context.documentId).fragments.Length; string imported = "";
        var review = documents.imports.PreviewText(documents.Read(context.documentId), "原生导入撤销验证");
        documents.Edit(context.documentId, "Import reviewed dialogue", state => imported = documents.imports.Append(state, review, "Imported", "imported"));
        if (!runtime.interactions.history.Undo().succeeded || documents.Read(context.documentId).fragments.Length != fragments
            || !runtime.interactions.history.Redo().succeeded || documents.Read(context.documentId).entries.Single(entry => entry.id == "imported").nodeId != imported
            || !runtime.interactions.documents.Save(context.documentId)) throw new InvalidOperationException("Reviewed dialogue import bypassed native History or lost stable identity during redo/save.");
        long revision = documents.GetRevision(context.documentId); string continued = "";
        documents.Edit(context.documentId, "Continue dialogue", state =>
        {
            var id = documents.edits.ContinueDialogue(state, new("hello")); continued = id.value;
            var node = state.graph.document.FindNode(id)!; var line = documents.codec.Read<DialogueNode>(node); line.text = "继续写下一句"; documents.codec.Write(node, line);
        });
        long editedRevision = documents.GetRevision(context.documentId);
        if (editedRevision <= revision || !runtime.interactions.history.Undo().succeeded || documents.GetRevision(context.documentId) <= editedRevision
            || documents.Read(context.documentId).graph.document.FindNode(new(continued)) is not null
            || !runtime.interactions.history.Redo().succeeded || documents.Read(context.documentId).graph.document.FindNode(new(continued)) is null
            || !runtime.interactions.documents.Save(context.documentId)) throw new InvalidOperationException("Continue-writing or native revision stamps lost undo/redo identity or failed source saving.");
        var moveDraft = documents.Read(context.documentId);
        var movePlan = documents.reorder.Preview(moveDraft, new(continued), documents.reorder.DropConnection(moveDraft, new(continued), new("hello"), false));
        string priorFirst = moveDraft.graph.document.edges.Single(static edge => edge.output.nodeId.value == "entry").input.nodeId.value;
        documents.Edit(context.documentId, "Move reviewed dialogue", candidate => documents.reorder.Apply(candidate, movePlan));
        if (documents.Read(context.documentId).graph.document.edges.Single(static edge => edge.output.nodeId.value == "entry").input.nodeId.value != continued
            || !runtime.interactions.history.Undo().succeeded || documents.Read(context.documentId).graph.document.edges.Single(static edge => edge.output.nodeId.value == "entry").input.nodeId.value != priorFirst
            || !runtime.interactions.history.Redo().succeeded || !runtime.interactions.documents.Save(context.documentId))
            throw new InvalidOperationException("Reviewed card movement bypassed native atomic History or source saving.");
        var groupSource = documents.Read(context.documentId); var groupLine = documents.codec.Read<DialogueNode>(groupSource.graph.document.FindNode(new("hello"))!);
        groupLine.text += " {player}"; documents.codec.Write(groupSource.graph.document.FindNode(new("hello"))!, groupLine);
        byte[] groupBytes = documents.clipboard.Capture(groupSource, [continued, "hello"]); string[] pastedGroup = [];
        if (documents.clipboard.Review(groupBytes).nodeIds.Count != 2) throw new InvalidOperationException("Native group clipboard review lost copied membership.");
        documents.Edit(context.documentId, "Paste reviewed group", candidate => pastedGroup = documents.clipboard.Paste(candidate, "opening", groupBytes, new(100, 100),
            variableTargets: new System.Collections.Generic.Dictionary<string, string> { ["player"] = "copied_player" }));
        if (pastedGroup.Length != 2 || pastedGroup.Any(id => documents.Read(context.documentId).graph.document.FindNode(new(id)) is null)
            || !documents.Read(context.documentId).GetVariables().Any(static variable => variable.id == "copied_player")
            || !runtime.interactions.history.Undo().succeeded || pastedGroup.Any(id => documents.Read(context.documentId).graph.document.FindNode(new(id)) is not null)
            || documents.Read(context.documentId).GetVariables().Any(static variable => variable.id == "copied_player")
            || !runtime.interactions.history.Redo().succeeded || pastedGroup.Any(id => documents.Read(context.documentId).graph.document.FindNode(new(id)) is null)
            || !runtime.interactions.documents.Save(context.documentId)) throw new InvalidOperationException("Reviewed group paste bypassed atomic native History or source saving.");
        if (!runtime.interactions.history.Undo().succeeded || !runtime.interactions.documents.Save(context.documentId))
            throw new InvalidOperationException("Restoring the original document after the native paste check failed.");
        var deleteReview = documents.deletion.Preview(documents.Read(context.documentId), [continued, "hello"]);
        documents.Edit(context.documentId, "Delete reviewed group", candidate => documents.deletion.Apply(candidate, deleteReview));
        if (documents.Read(context.documentId).graph.document.FindNode(new("hello")) is not null || documents.Read(context.documentId).graph.document.FindNode(new(continued)) is not null
            || !runtime.interactions.documents.Save(context.documentId) || !runtime.interactions.history.Undo().succeeded
            || documents.Read(context.documentId).graph.document.FindNode(new("hello")) is null || documents.Read(context.documentId).graph.document.FindNode(new(continued)) is null
            || !runtime.interactions.history.Redo().succeeded || documents.Read(context.documentId).graph.document.FindNode(new("hello")) is not null
            || !runtime.interactions.documents.Save(context.documentId)) throw new InvalidOperationException("Reviewed group deletion bypassed native History or source saving.");
        if (!runtime.interactions.history.Undo().succeeded || !runtime.interactions.documents.Save(context.documentId))
            throw new InvalidOperationException("Restoring the native deletion fixture failed.");
        var page = resources.Create(new ScreenAsset { root = new() { id = "root", kind = UiElementKind.Panel } });
        string childId = string.Empty;
        resources.Edit<ScreenAsset>(page.documentId, "Add UI button", source => childId = ScreenEditService.Add(source, "root", UiElementKind.Button));
        if (!page.isDirty || resources.Read<ScreenAsset>(page.documentId).root.children.Length != 1) throw new InvalidOperationException("Native UI document edit did not update its shared draft.");
        if (!runtime.interactions.history.Undo().succeeded || page.isDirty || resources.Read<ScreenAsset>(page.documentId).root.children.Length != 0) throw new InvalidOperationException("UI Undo did not restore the saved tree.");
        if (!runtime.interactions.history.Redo().succeeded || resources.Read<ScreenAsset>(page.documentId).root.children[0].id != childId) throw new InvalidOperationException("UI Redo changed stable element identity.");
        resources.Edit<ScreenAsset>(page.documentId, "Add persistent page field", source => UiStateEditService.Set(source,
            new() { id = "ui-note", name = "UI note", scope = Model.Values.VariableScope.Game, initialValue = Model.Values.NovelValue.String("native state") }));
        if (!runtime.interactions.history.Undo().succeeded || resources.Read<ScreenAsset>(page.documentId).stateVariables.Length != 0
            || !runtime.interactions.history.Redo().succeeded || resources.Read<ScreenAsset>(page.documentId).stateVariables.Single().id != "ui-note")
            throw new InvalidOperationException("Page state fields bypassed native document History.");
        resources.Edit<ScreenAsset>(page.documentId, "Explain UI control", source =>
        {
            var element = ScreenEditService.Find(source, childId); element.tooltip = "Saved native help"; element.tooltipBinding = "page.ui-note";
        });
        if (!runtime.interactions.history.Undo().succeeded || resources.Read<ScreenAsset>(page.documentId).root.children[0].tooltip.Length != 0
            || !runtime.interactions.history.Redo().succeeded || resources.Read<ScreenAsset>(page.documentId).root.children[0].tooltip != "Saved native help")
            throw new InvalidOperationException("Tooltip edits bypassed native document History.");
        resources.Edit<ScreenAsset>(page.documentId, "Animate UI control", source =>
        {
            var motion = ScreenEditService.Find(source, childId).animation.enter;
            motion.enabled = true; motion.duration = .35; motion.pose.y = 24; motion.pose.opacity = 0;
        });
        if (!runtime.interactions.history.Undo().succeeded || resources.Read<ScreenAsset>(page.documentId).root.children[0].animation.enter.enabled
            || !runtime.interactions.history.Redo().succeeded || resources.Read<ScreenAsset>(page.documentId).root.children[0].animation.enter.duration != .35)
            throw new InvalidOperationException("UI animation edits bypassed native document History.");
        var canvas = resources.Read<ScreenAsset>(page.documentId);
        var canvasStart = UiCanvasEditService.Capture(canvas, childId, UiCanvasContracts.Bounds(canvas, childId));
        float canvasX = canvas.root.children[0].layout.x;
        using (var gesture = new EditorEditGesture(runtime.interactions.history, "Canvas drag"))
        {
            resources.Edit<ScreenAsset>(page.documentId, "Canvas drag", candidate => UiCanvasEditService.Transform(candidate, canvasStart, 10, 20), gesture.mergeKey);
            resources.Edit<ScreenAsset>(page.documentId, "Canvas drag", candidate => UiCanvasEditService.Transform(candidate, canvasStart, 30, 40), gesture.mergeKey);
            gesture.Commit();
        }
        if (resources.Read<ScreenAsset>(page.documentId).root.children[0].layout.x != canvasX + 30
            || !runtime.interactions.history.Undo().succeeded || resources.Read<ScreenAsset>(page.documentId).root.children[0].layout.x != canvasX
            || !runtime.interactions.history.Redo().succeeded || resources.Read<ScreenAsset>(page.documentId).root.children[0].layout.x != canvasX + 30)
            throw new InvalidOperationException("Canvas samples did not form one atomic native History gesture.");
        canvas = resources.Read<ScreenAsset>(page.documentId); canvasStart = UiCanvasEditService.Capture(canvas, childId, UiCanvasContracts.Bounds(canvas, childId));
        using (var gesture = new EditorEditGesture(runtime.interactions.history, "Cancelled canvas resize"))
        {
            resources.Edit<ScreenAsset>(page.documentId, "Cancelled canvas resize", candidate => UiCanvasEditService.Transform(candidate, canvasStart, 20, 0, UiResizeEdges.Right), gesture.mergeKey);
            if (!gesture.Cancel().succeeded || resources.Read<ScreenAsset>(page.documentId).root.children[0].layout.width != canvas.root.children[0].layout.width)
                throw new InvalidOperationException("Cancelling a native canvas resize retained its partial size edit.");
        }
        canvas = resources.Read<ScreenAsset>(page.documentId); canvasStart = UiCanvasEditService.Capture(canvas, childId, UiCanvasContracts.Bounds(canvas, childId));
        resources.Edit<ScreenAsset>(page.documentId, "Align canvas child", candidate => UiCanvasEditService.Align(candidate, canvasStart,
            new Inno.Core.Mathematics.Rect(0, 0, canvas.width, canvas.height), UiCanvasAlignment.HorizontalCenter));
        if (Math.Abs(resources.Read<ScreenAsset>(page.documentId).root.children[0].layout.x - (canvas.width - canvas.root.children[0].layout.width) / 2) > .001
            || !runtime.interactions.history.Undo().succeeded || resources.Read<ScreenAsset>(page.documentId).root.children[0].layout.x != canvas.root.children[0].layout.x
            || !runtime.interactions.history.Redo().succeeded)
            throw new InvalidOperationException("Canvas alignment bypassed actual layout or native Undo/Redo.");
        resources.Edit<ScreenAsset>(page.documentId, "Edit native margins and alignment", source =>
        {
            var layout = source.root.children[0].layout; layout.marginLeft = 11; layout.marginTop = 7; layout.marginRight = 13; layout.marginBottom = 17;
            layout.horizontalAlignment = UiAlignment.Center; layout.verticalAlignment = UiAlignment.End; source.root.layout.flowAlignment = UiFlowAlignment.SpaceAround;
        });
        if (!runtime.interactions.history.Undo().succeeded || resources.Read<ScreenAsset>(page.documentId).root.children[0].layout.marginLeft != 0
            || !runtime.interactions.history.Redo().succeeded || resources.Read<ScreenAsset>(page.documentId).root.children[0].layout.marginBottom != 17)
            throw new InvalidOperationException("UI margins and alignment bypassed native document History.");
        byte[] reload = resources.CaptureReload();
        resources.RestoreReload(reload);
        if (!runtime.interactions.documents.Save(page.documentId) || page.isDirty) throw new InvalidOperationException("Native UI source save failed.");
        var actions = resources.Create(new Model.Interface.Actions.UiActionCatalogAsset());
        resources.Edit<Model.Interface.Actions.UiActionCatalogAsset>(actions.documentId, "Add typed UI action", candidate => UiActionCatalogEditService.Set(candidate,
            new() { id = "custom.native-history", title = "Native action", argument = new() { id = "argument", defaultValue = Model.Values.NovelValue.Integer(3) } }));
        if (!runtime.interactions.history.Undo().succeeded || resources.Read<Model.Interface.Actions.UiActionCatalogAsset>(actions.documentId).definitions.Length != 0
            || !runtime.interactions.history.Redo().succeeded || !runtime.interactions.documents.Save(actions.documentId))
            throw new InvalidOperationException("Custom UI action metadata bypassed native History or source saving.");
        var attached = new Model.Project.NovelProjectAsset();
        Inno.VisualNovel.Authoring.ProjectEditing.NovelProjectEditService.Attach(attached, Inno.Assets.Assets.Load<Model.Interface.Actions.UiActionCatalogAsset>(AssetPath.Parse(actions.assetPath)));
        if (attached.uiActions.Single().argument.defaultValue.integer != 3) throw new InvalidOperationException("The native action catalog did not attach typed metadata to the game.");
        var sourceCatalog = resources.Create(new Model.Interface.Data.UiDataSourceCatalogAsset());
        resources.Edit<Model.Interface.Data.UiDataSourceCatalogAsset>(sourceCatalog.documentId, "Add typed UI source", candidate => UiDataSourceCatalogEditService.Set(candidate,
            new() { id = "native", title = "Native source", fields = [new() { schema = new() { id = "name", defaultValue = Model.Values.NovelValue.String("native") } }] }));
        if (!runtime.interactions.history.Undo().succeeded || resources.Read<Model.Interface.Data.UiDataSourceCatalogAsset>(sourceCatalog.documentId).definitions.Length != 0
            || !runtime.interactions.history.Redo().succeeded || !runtime.interactions.documents.Save(sourceCatalog.documentId))
            throw new InvalidOperationException("Typed UI source metadata bypassed native History or saving.");
        Inno.VisualNovel.Authoring.ProjectEditing.NovelProjectEditService.Attach(attached, Inno.Assets.Assets.Load<Model.Interface.Data.UiDataSourceCatalogAsset>(AssetPath.Parse(sourceCatalog.assetPath)));
        if (attached.uiDataSources.Single().fields.Single().schema.defaultValue.text != "native") throw new InvalidOperationException("The native source catalog did not attach exact typed metadata.");
        var licenses = resources.Create(new Inno.VisualNovel.Authoring.AssetAuditing.Licenses.ResourceLicenseCatalogAsset());
        var licensedPage = Inno.Assets.Assets.Load<ScreenAsset>(AssetPath.Parse(page.assetPath));
        resources.Edit<Inno.VisualNovel.Authoring.AssetAuditing.Licenses.ResourceLicenseCatalogAsset>(licenses.documentId, "Record resource provenance", candidate =>
            Inno.VisualNovel.Authoring.AssetAuditing.Licenses.ResourceLicenseEditService.Set(candidate, new() { asset = licensedPage, author = "Native author", license = "Fixture permission" }));
        if (!runtime.interactions.history.Undo().succeeded || resources.Read<Inno.VisualNovel.Authoring.AssetAuditing.Licenses.ResourceLicenseCatalogAsset>(licenses.documentId).entries.Length != 0
            || !runtime.interactions.history.Redo().succeeded || !runtime.interactions.documents.Save(licenses.documentId))
            throw new InvalidOperationException("Provenance edits bypassed native History or rejected a structurally valid incomplete record.");
        string resourcePath = page.assetPath;
        var labels = resources.Create(new Inno.VisualNovel.Authoring.AssetOrganization.ResourceLabelCatalogAsset());
        resources.Edit<Inno.VisualNovel.Authoring.AssetOrganization.ResourceLabelCatalogAsset>(labels.documentId, "Label native page", candidate =>
            Inno.VisualNovel.Authoring.AssetOrganization.ResourceLabelEditService.Set(candidate, new() { asset = licensedPage, group = "界面", tags = ["教学"] }));
        if (!runtime.interactions.history.Undo().succeeded || resources.Read<Inno.VisualNovel.Authoring.AssetOrganization.ResourceLabelCatalogAsset>(labels.documentId).entries.Length != 0
            || !runtime.interactions.history.Redo().succeeded || !runtime.interactions.documents.Save(labels.documentId))
            throw new InvalidOperationException("Native resource labels bypassed ordinary document History or saving.");
        resources.Edit<Inno.VisualNovel.Authoring.AssetOrganization.ResourceLabelCatalogAsset>(labels.documentId, "Batch resource labels", candidate =>
            Inno.VisualNovel.Authoring.AssetOrganization.ResourceLabelEditService.ApplyBatch(candidate, [licensedPage.identity.persistentId], new() { replaceGroup = true, group = "菜单", addTags = ["共享"] }));
        if (!runtime.interactions.history.Undo().succeeded || resources.Read<Inno.VisualNovel.Authoring.AssetOrganization.ResourceLabelCatalogAsset>(labels.documentId).entries[0].group != "界面"
            || !runtime.interactions.history.Redo().succeeded || !runtime.interactions.documents.Save(labels.documentId))
            throw new InvalidOperationException("A resource label batch was not one reversible native document edit.");
        string labelsPath = labels.assetPath;
        runtime.interactions.documents.Close(labels.documentId, EditorDocumentCloseMode.Save);
        var reopenedLabels = resources.Open(AssetPath.Parse(labelsPath));
        if (resources.Read<Inno.VisualNovel.Authoring.AssetOrganization.ResourceLabelCatalogAsset>(reopenedLabels.documentId).entries[0].tags.Length != 2)
            throw new InvalidOperationException("Resource label source reopening lost the batch metadata.");
        runtime.interactions.documents.Close(page.documentId, EditorDocumentCloseMode.Save);
        var reopened = resources.Open(AssetPath.Parse(resourcePath));
        if (resources.Read<ScreenAsset>(reopened.documentId).root.children[0].id != childId) throw new InvalidOperationException("Saved UI source reopening lost stable identity.");
        var reopenedLayout = resources.Read<ScreenAsset>(reopened.documentId).root.children[0].layout;
        if (reopenedLayout.marginLeft != 11 || reopenedLayout.marginRight != 13 || reopenedLayout.marginTop != 7 || reopenedLayout.marginBottom != 17
            || reopenedLayout.horizontalAlignment != UiAlignment.Center || reopenedLayout.verticalAlignment != UiAlignment.End
            || resources.Read<ScreenAsset>(reopened.documentId).root.layout.flowAlignment != UiFlowAlignment.SpaceAround)
            throw new InvalidOperationException("Native page save/reload lost margin or alignment declarations.");
        if (resources.Read<ScreenAsset>(reopened.documentId).stateVariables.Single().initialValue.text != "native state") throw new InvalidOperationException("Native page save/reload lost declared persistent UI state.");
        if (resources.Read<ScreenAsset>(reopened.documentId).root.children[0].tooltip != "Saved native help"
            || resources.Read<ScreenAsset>(reopened.documentId).root.children[0].tooltipBinding != "page.ui-note") throw new InvalidOperationException("Native page save/reload dropped tooltip content or its page-state binding.");
        if (!resources.Read<ScreenAsset>(reopened.documentId).root.children[0].animation.enter.enabled
            || resources.Read<ScreenAsset>(reopened.documentId).root.children[0].animation.enter.pose.y != 24)
            throw new InvalidOperationException("Native page save/reload dropped authored animation channels.");
        ScreenElementSelection.Select(runtime.interactions, reopened.documentId, childId);
        if (!ScreenElementSelection.TryRead(runtime.interactions, reopened.documentId, out var selectedElement) || selectedElement != childId
            || ScreenElementSelection.TryRead(runtime.interactions, Guid.NewGuid(), out _) || StoryNodeSelection.TryRead(runtime.interactions, reopened.documentId, out _))
            throw new InvalidOperationException("Shared UI selection crossed a native document or narrative domain.");
        resources.Edit<ScreenAsset>(reopened.documentId, "Delete selected UI element", source => ScreenEditService.Remove(source, childId));
        string resolved = ScreenElementSelection.Resolve(runtime.interactions, reopened.documentId, resources.Read<ScreenAsset>(reopened.documentId), childId);
        if (resolved != "root" || !ScreenElementSelection.TryRead(runtime.interactions, reopened.documentId, out selectedElement) || selectedElement != "root"
            || !runtime.interactions.history.Undo().succeeded || resources.Read<ScreenAsset>(reopened.documentId).root.children[0].id != childId)
            throw new InvalidOperationException("UI selection did not retire a deleted identity or preserve native Undo restoration.");
        string groupId = string.Empty;
        resources.Edit<ScreenAsset>(reopened.documentId, "Create UI group", source => groupId = ScreenEditService.Add(source, "root", UiElementKind.Vertical));
        ScreenElementSelection.Select(runtime.interactions, reopened.documentId, childId);
        resources.Edit<ScreenAsset>(reopened.documentId, "Change UI parent", source =>
        {
            var target = UiHierarchyDropService.Describe(source, childId, groupId, EditorDropPlacement.Into);
            ScreenEditService.Reparent(source, target.sourceId, target.parentId, target.index);
        });
        if (ScreenEditService.Flatten(resources.Read<ScreenAsset>(reopened.documentId)).Single(row => row.element.id == childId).parent?.id != groupId
            || !ScreenElementSelection.TryRead(runtime.interactions, reopened.documentId, out selectedElement) || selectedElement != childId
            || !runtime.interactions.history.Undo().succeeded || resources.Read<ScreenAsset>(reopened.documentId).root.children[0].id != childId
            || !runtime.interactions.history.Redo().succeeded || !runtime.interactions.documents.Save(reopened.documentId))
            throw new InvalidOperationException("Hierarchy editing lost native selection, atomic Undo/Redo or source saving.");
        runtime.interactions.documents.Close(reopened.documentId, EditorDocumentCloseMode.Save);
        reopened = resources.Open(AssetPath.Parse(resourcePath));
        if (ScreenEditService.Flatten(resources.Read<ScreenAsset>(reopened.documentId)).Single(row => row.element.id == childId).parent?.id != groupId
            || ScreenEditService.Find(resources.Read<ScreenAsset>(reopened.documentId), childId).tooltipBinding != "page.ui-note")
            throw new InvalidOperationException("Native hierarchy reopening lost ownership or the moved subtree's binding.");
        var timeline = resources.Create(TimelineSelectionContracts.Clip());
        var selectedKeys = Inno.VisualNovel.Authoring.TimelineEditing.PerformanceKeySelection.Capture(resources.Read<Model.Performance.PerformanceClipAsset>(timeline.documentId), [new(0, 1), new(0, 2), new(1, 1.5)]);
        using (var gesture = new EditorEditGesture(runtime.interactions.history, "Native key group drag"))
        {
            resources.Edit<Model.Performance.PerformanceClipAsset>(timeline.documentId, "Native key group drag", candidate => candidate.tracks = selectedKeys.Transform(.5), gesture.mergeKey);
            resources.Edit<Model.Performance.PerformanceClipAsset>(timeline.documentId, "Native key group drag", candidate => candidate.tracks = selectedKeys.Transform(1), gesture.mergeKey);
            gesture.Commit();
        }
        if (!runtime.interactions.history.Undo().succeeded || resources.Read<Model.Performance.PerformanceClipAsset>(timeline.documentId).tracks[0].keys[1].time != 1
            || !runtime.interactions.history.Redo().succeeded || resources.Read<Model.Performance.PerformanceClipAsset>(timeline.documentId).tracks[1].keys[1].time != 2.5)
            throw new InvalidOperationException("Multi-track keys did not form one native History gesture.");
        var cancelKeys = Inno.VisualNovel.Authoring.TimelineEditing.PerformanceKeySelection.Capture(resources.Read<Model.Performance.PerformanceClipAsset>(timeline.documentId), [new(0, 2)]);
        using (var gesture = new EditorEditGesture(runtime.interactions.history, "Cancelled native key group"))
        {
            resources.Edit<Model.Performance.PerformanceClipAsset>(timeline.documentId, "Cancelled native key group", candidate => candidate.tracks = cancelKeys.Transform(.5), gesture.mergeKey);
            if (!gesture.Cancel().succeeded || resources.Read<Model.Performance.PerformanceClipAsset>(timeline.documentId).tracks[0].keys[1].time != 2)
                throw new InvalidOperationException("Cancelling key movement left a partial authored sample.");
        }
        string timelinePath = timeline.assetPath;
        if (!runtime.interactions.documents.Save(timeline.documentId)) throw new InvalidOperationException("Key group changes did not save through the native clip importer.");
        runtime.interactions.documents.Close(timeline.documentId, EditorDocumentCloseMode.Save);
        timeline = resources.Open(AssetPath.Parse(timelinePath));
        if (resources.Read<Model.Performance.PerformanceClipAsset>(timeline.documentId).tracks[0].keys[2].time != 3)
            throw new InvalidOperationException("Native clip reopening lost multi-key changes.");
        var effect = resources.Create(new Inno.VisualNovel.Model.Performance.VisualEffectAsset { resourceId = "history-rain" });
        resources.Edit<Inno.VisualNovel.Model.Performance.VisualEffectAsset>(effect.documentId, "Edit particle rate", static source => source.particles.rate = 35);
        if (!runtime.interactions.history.Undo().succeeded || resources.Read<Inno.VisualNovel.Model.Performance.VisualEffectAsset>(effect.documentId).particles.rate != 20)
            throw new InvalidOperationException("Particle resource undo did not restore native nested parameters.");
        if (!runtime.interactions.history.Redo().succeeded || !runtime.interactions.documents.Save(effect.documentId)) throw new InvalidOperationException("Particle resource redo or native save failed.");
        var locale = resources.Create(new Inno.VisualNovel.Model.Localization.LocalizationTableAsset());
        resources.Edit<Inno.VisualNovel.Model.Localization.LocalizationTableAsset>(locale.documentId, "Add game language", static source =>
            Inno.VisualNovel.Authoring.LocalizationEditing.LocalizationEditService.SetLanguage(source, "en", "English"));
        if (!runtime.interactions.history.Undo().succeeded || resources.Read<Inno.VisualNovel.Model.Localization.LocalizationTableAsset>(locale.documentId).languages.Length != 1
            || !runtime.interactions.history.Redo().succeeded || !runtime.interactions.documents.Save(locale.documentId)) throw new InvalidOperationException("Localization native History/save did not retain the declared languages.");
        var translationPreview = Inno.VisualNovel.Authoring.LocalizationEditing.LocalizationDelimitedText.Preview(
            resources.Read<Model.Localization.LocalizationTableAsset>(locale.documentId),
            "id,source,language,translation,status,sourceFingerprint,voice\nimported-history,原文,en,Imported translation,Reviewed,,");
        resources.Edit<Model.Localization.LocalizationTableAsset>(locale.documentId, "Import reviewed translations", source =>
            Inno.VisualNovel.Authoring.LocalizationEditing.LocalizationDelimitedText.Apply(source, translationPreview));
        if (!runtime.interactions.history.Undo().succeeded || resources.Read<Model.Localization.LocalizationTableAsset>(locale.documentId).entries.Length != 0
            || !runtime.interactions.history.Redo().succeeded || !runtime.interactions.documents.Save(locale.documentId))
            throw new InvalidOperationException("Reviewed translation import did not share native History and saving.");
        string localePath = locale.assetPath;
        runtime.interactions.documents.Close(locale.documentId, EditorDocumentCloseMode.Save);
        locale = resources.Open(AssetPath.Parse(localePath));
        var reloadedLocale = resources.Read<Model.Localization.LocalizationTableAsset>(locale.documentId);
        if (new Player.Localization.NovelLocalizationService(reloadedLocale, "en").Resolve("imported-history", "原文") != "Imported translation")
            throw new InvalidOperationException("Native translation reopening lost reviewed runtime text.");
        var collection = resources.Create(new Inno.VisualNovel.Model.Collections.CollectionAsset());
        resources.Edit<Inno.VisualNovel.Model.Collections.CollectionAsset>(collection.documentId, "Add gallery item", static source =>
            Inno.VisualNovel.Authoring.CollectionEditing.CollectionEditService.Add(source, "ending.first", Inno.VisualNovel.Model.Collections.CollectionKind.Ending));
        if (!runtime.interactions.history.Undo().succeeded || resources.Read<Inno.VisualNovel.Model.Collections.CollectionAsset>(collection.documentId).entries.Length != 0
            || !runtime.interactions.history.Redo().succeeded || !runtime.interactions.documents.Save(collection.documentId)) throw new InvalidOperationException("Collection native History/save did not retain unlock identities.");
        Console.WriteLine("History acceptance project: " + root);
        GameSearchContracts.NativeInspection(documents, runtime.interactions, context);
        var beforeLibrary = documents.Read(context.documentId);
        var sharedDocument = resources.Create(new Model.Values.VariableTableAsset { variables = beforeLibrary.variables
            .Where(static item => item.scope != Model.Values.VariableScope.Local).Select(Model.Values.VariableValueRules.Copy).ToArray() });
        string originalVariableGroup = resources.Read<Model.Values.VariableTableAsset>(sharedDocument.documentId).variables[0].group;
        resources.Edit<Model.Values.VariableTableAsset>(sharedDocument.documentId, "Document shared variable", candidate =>
        {
            var declaration = Model.Values.VariableValueRules.Copy(candidate.variables[0]); declaration.group = "原生分组"; declaration.description = "原生用途说明";
            Authoring.Variables.VariableTableEditService.Set(candidate, declaration);
        });
        if (!runtime.interactions.history.Undo().succeeded || resources.Read<Model.Values.VariableTableAsset>(sharedDocument.documentId).variables[0].group != originalVariableGroup
            || !runtime.interactions.history.Redo().succeeded || !runtime.interactions.documents.Save(sharedDocument.documentId))
            throw new InvalidOperationException("Variable metadata bypassed native History or source saving.");
        var sharedTable = Inno.Assets.Assets.Load<Model.Values.VariableTableAsset>(AssetPath.Parse(sharedDocument.assetPath));
        if (sharedTable.variables[0].description != "原生用途说明") throw new InvalidOperationException("Native shared library import dropped author documentation.");
        documents.Edit(context.documentId, "Attach shared declarations", candidate => Authoring.Variables.StoryVariableBindingService.Attach(candidate, sharedTable, true));
        if (documents.Read(context.documentId).variableTableId != sharedTable.identity.persistentId
            || documents.Read(context.documentId).GetVariables().Length != beforeLibrary.variables.Length
            || !runtime.interactions.history.Undo().succeeded || documents.Read(context.documentId).variableTableId != Guid.Empty
            || !runtime.interactions.history.Redo().succeeded || !runtime.interactions.documents.Save(context.documentId))
            throw new InvalidOperationException("Shared library attachment bypassed neutral chapter History, duplicated definitions or failed source import.");
        string sharedStoryPath = context.assetPath;
        runtime.interactions.documents.Close(context.documentId, EditorDocumentCloseMode.Save);
        var linked = documents.Open(AssetPath.Parse(sharedStoryPath));
        var linkedSource = documents.Read(linked.documentId);
        if (linkedSource.variableTableId != sharedTable.identity.persistentId || linkedSource.variableTablePath != sharedTable.assetPath.ToString()
            || !assets.GetDependencies(Inno.Assets.Assets.Load<Model.Story.StoryAsset>(AssetPath.Parse(sharedStoryPath)), true).Any(dependency => dependency.persistentId == sharedTable.identity.persistentId))
            throw new InvalidOperationException("Native chapter reopening lost library identity, known path or its actual importer dependency.");
        var copiedSource = Authoring.Templates.FirstStoryTemplate.Create(documents.codec);
        Authoring.Variables.StoryVariableBindingService.Attach(copiedSource, sharedTable, true);
        var copiedTarget = Authoring.Templates.FirstStoryTemplate.Create(documents.codec);
        Authoring.Variables.StoryVariableBindingService.Attach(copiedTarget, sharedTable, true);
        byte[] copiedLine = documents.clipboard.Capture(copiedSource, ["hello"]);
        documents.clipboard.Paste(copiedTarget, "opening", copiedLine, new(0, 0), new System.Collections.Generic.Dictionary<string, string> { ["choice"] = "choice" });
        if (copiedTarget.variables.Any(static item => item.id == "player") || !documents.compiler.Compile(copiedTarget.ToAsset()).success)
            throw new InvalidOperationException("Graph clipboard duplicated a library-owned declaration or failed to retain its typed source reference.");
        documents.Edit(linked.documentId, "Detach preserving declarations", Authoring.Variables.StoryVariableBindingService.Detach);
        if (documents.Read(linked.documentId).variableTableId != Guid.Empty || documents.Read(linked.documentId).GetVariables().Length != linkedSource.GetVariables().Length
            || !runtime.interactions.history.Undo().succeeded || documents.Read(linked.documentId).variableTableId != sharedTable.identity.persistentId)
            throw new InvalidOperationException("Detaching a library lost declarations or native Undo identity.");
        resources.Edit<Model.Values.VariableTableAsset>(sharedDocument.documentId, "Update saved shared default", candidate => candidate.variables.Single(static item => item.id == "player").initialValue = Model.Values.NovelValue.String("Native shared update"));
        if (!runtime.interactions.documents.Save(sharedDocument.documentId)) throw new InvalidOperationException("The updated native shared library did not save.");
        var updatedStory = Inno.Assets.Assets.Load<Model.Story.StoryAsset>(AssetPath.Parse(sharedStoryPath));
        if (updatedStory.program!.variables.Single(static item => item.id == "player").initialValue.text != "Native shared update")
            throw new InvalidOperationException("Native dependency invalidation did not rebuild a chapter after saving its shared declaration library.");

        string Line() => documents.codec.Read<DialogueNode>(documents.Read(context.documentId).graph.document.FindNode(new("hello"))!).text;
    }
}

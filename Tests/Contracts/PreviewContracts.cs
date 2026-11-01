using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.Text;
using Inno.VisualNovel.Authoring.Preview;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Sessions;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class PreviewContracts
{
    internal static void DraftAndPreconditions(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var draft = FirstStoryTemplate.Create(codec); draft.variables = [.. draft.variables, new() { id = "global", scope = VariableScope.Global, initialValue = NovelValue.Boolean(false) }];
        var source = draft.ToAsset(); source.program = compiler.Compile(source).program!; var context = Context(source);
        using var caller = new NovelSession(context); caller.StartNew();
        string fingerprint = source.program.fingerprint;
        var edits = new StoryEditService(codec);
        var camera = edits.Add(draft, "opening", StoryNodeIds.stage, new StageNode { operation = StageOperation.SetCamera, transform = new() { x = 200 } }, new(100, 200));
        edits.Connect(draft, new(new("entry"), new("next")), camera); edits.Connect(draft, new(camera, new("next")), new("hello"));
        byte[] before = native.Serialize(draft);
        var test = new NovelPreviewCaseAsset { mode = PreviewRunMode.Local, nodeId = camera.value, useStageSnapshot = true, stage = new() { cameraX = 10 },
            initialValues = [new() { id = "player", value = NovelValue.String("测试姓名") }, new() { id = "global", value = NovelValue.Boolean(true) }] };
        using var preview = new NovelPreviewSession(context, draft, compiler, test, static (_, text) => (text.Length * 10, 40));
        Check(preview.session.isPreview && preview.session.story!.status == StoryStatus.Dialogue && preview.session.story.dialogueText.Contains("测试姓名", StringComparison.Ordinal), "Local preview bypassed the production interpreter or lost typed initial values.");
        Check(preview.session.stage.Capture().cameraX == 200, "Initial stage was applied after the first preview presentation instruction.");
        Check(preview.view.BuildFrame().items.Any(static item => item.id == "line") && preview.view.persistence is null, "Authoring preview did not use the real editable UI or attached player storage.");
        Check(preview.session.story!.GetVariable("global").boolean && !caller.story!.GetVariable("global").boolean && caller.story.GetVariable("player").text == "旅人", "Preview modified another session's globals or variables.");
        test.initialValues[0].value = NovelValue.String("外部修改");
        Check(preview.session.story!.GetVariable("player").text == "测试姓名" && native.Serialize(draft).AsSpan().SequenceEqual(before) && source.program.fingerprint == fingerprint,
            "Preview retained editable case aliases or mutated canonical content and graph identity.");
        var restored = native.Deserialize<NovelSessionState>(native.Serialize(preview.session.Capture())); preview.session.Restore(restored);
        Check(preview.session.story!.dialogueText.Contains("测试姓名", StringComparison.Ordinal), "Production native snapshots failed inside an isolated preview.");
    }

    internal static void RecordedRoutes(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var draft = FirstStoryTemplate.Create(codec); var source = draft.ToAsset(); source.program = compiler.Compile(source).program!; var context = Context(source);
        var test = new NovelPreviewCaseAsset { mode = PreviewRunMode.Route, nodeId = "sea", choices = ["sea"] };
        using var route = new NovelPreviewSession(context, draft, compiler, test, static (_, _) => (100, 40)); test.choices[0] = "forest";
        route.Tick(0);
        Check(route.targetReached && !route.routeRunning && route.error.Length == 0 && route.session.story!.sourceNodeId == "sea" && !route.session.story.isTextComplete,
            "Recorded route re-evaluated editable choices or advanced through its interactive target.");
        using var missing = new NovelPreviewSession(context, draft, compiler, new() { mode = PreviewRunMode.Route, nodeId = "forest", choices = ["missing"] }, static (_, _) => (100, 40));
        missing.Tick(0); Check(!missing.targetReached && missing.error.Contains("missing", StringComparison.Ordinal) && missing.session.story!.status == StoryStatus.Choice, "Missing route decisions did not retain an actionable inspectable boundary.");
        var edits = new StoryEditService(codec); var groups = new ParallelEditService(edits, codec); var fork = groups.Create(draft, "opening", new(50, 400));
        var branch = draft.graph.document.edges.First(edge => edge.output.nodeId == fork).input.nodeId.value;
        edits.Connect(draft, new(new("entry"), new("next")), fork);
        var owner = codec.Read<Model.Story.Nodes.Flow.ParallelNode>(draft.graph.document.FindNode(fork)!); edits.Connect(draft, new(new(owner.joinId), new("next")), new("hello"));
        bool rejected = false;
        try { _ = PreviewContentBuilder.Build(context, draft, compiler, new() { mode = PreviewRunMode.Local, nodeId = branch }); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Local preview silently bypassed the production compiler's owned parallel entry rules.");
        var unrelated = FirstStoryTemplate.Create(codec); unrelated.chapterId = "unrelated-draft";
        var invalidLine = unrelated.graph.document.FindNode(new("hello"))!;
        var invalidSettings = codec.Read<Model.Story.Nodes.Reading.DialogueNode>(invalidLine); invalidSettings.charactersPerSecond = -1; codec.Write(invalidLine, invalidSettings);
        var complete = PreviewContentBuilder.Build(context, unrelated, compiler, new() { mode = PreviewRunMode.Complete });
        Check(complete.project.chapters.Length == context.chapters.Length && !complete.project.chapters.Any(item => item.chapterId == unrelated.chapterId),
            "Complete game preview appended or compiled an unrelated open draft.");
        rejected = false; try { PreviewContentBuilder.Build(context, unrelated, compiler, new() { mode = PreviewRunMode.Route, nodeId = "hello" }); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Route preview silently made an unassociated target chapter part of the game.");
    }

    private static NovelProjectAsset Context(Model.Story.StoryAsset chapter) => new()
    {
        chapters = [chapter], startChapter = chapter.chapterId,
        screens = new[] { "title", "reading", "pause", "settings", "history", "credits" }.Select(role => new NovelScreenBinding { role = role, screen = ShellScreenTemplates.Create(role, new FontAsset(), "Clear") }).ToArray()
    };
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}

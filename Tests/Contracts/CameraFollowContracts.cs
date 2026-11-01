using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.Rendering.Assets;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Stage.Composition;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class CameraFollowContracts
{
    internal static void PureTimelineAnchorsAndNativeFreeze(SerializationRegistry native)
    {
        var scene = new StageAsset { width = 800, height = 600, layers = [new() { id = "back", width = 800, height = 600 },
            new() { id = "target", width = 100, height = 100, parallax = .5, transform = new() { x = 300, y = 200 } }] };
        var motion = new PerformanceClipAsset { resourceId = "motion", tracks = [new() { target = "subject", property = PerformanceProperty.X,
            keys = [new() { time = 0, value = 0 }, new() { time = 1, value = 100 }] }, new() { target = "camera", property = PerformanceProperty.ScaleX,
            keys = [new() { time = 0, value = 1 }, new() { time = 1, value = 1.5 }] }] };
        var stage = new StageController([], [motion]); stage.SetStage(scene);
        stage.FollowCamera("stage/target", new() { pivotX = .25, pivotY = .75, scaleX = 2, rotation = 90 }, 800, 600);
        stage.ApplyPerformance(new() { operation = PerformanceOperation.Play, instanceId = "move", clipId = "motion", bindings = [new() { slot = "subject", target = "stage/target" }, new() { slot = "camera", target = "camera" }] });
        stage.Tick(.5); var half = stage.BuildFrame(800, 600).items.Single(item => item.id == "stage/target");
        Check(Near(half.topLeft.X, 200) && Near(half.topLeft.Y, 450), "Camera follow did not retain the normalized screen anchor through target motion zoom and rotation.");
        var saved = native.Deserialize<StageState>(native.Serialize(stage.Capture()));
        stage.Tick(.2); stage.Restore(saved);
        Check(stage.BuildFrame(800, 600).items.Single(item => item.id == "stage/target").topLeft == half.topLeft, "Native restore changed deterministic camera following.");
        saved.cameraFollow!.anchorX = double.NaN;
        Reject(() => stage.Restore(saved));
        Check(stage.cameraFollowOwner == "stage/target" && stage.BuildFrame(800, 600).items.Single(item => item.id == "stage/target").topLeft == half.topLeft,
            "An invalid follow candidate replaced the current camera.");
        stage.StopCameraFollow();
        Check(stage.cameraFollowOwner.Length == 0 && Same(half, stage.BuildFrame(800, 600).items.Single(item => item.id == "stage/target")),
            "Stopping follow jumped the camera or applied camera channels twice.");
        stage.FollowCamera("stage/target", new() { pivotX = .5, pivotY = .5 }, 800, 600);
        var before = stage.BuildFrame(800, 600).items.Single(item => item.id == "stage/back");
        stage.SetStage(new() { layers = [new() { id = "back", width = 800, height = 600 }] });
        Check(stage.cameraFollowOwner.Length == 0 && Same(before, stage.BuildFrame(800, 600).items.Single()), "Removing a followed scenery target changed the frozen background framing.");
        Reject(() => stage.FollowCamera("camera", new() { pivotX = .5, pivotY = .5 }, 800, 600));
        Reject(() => stage.FollowCamera("stage/absent", new() { pivotX = .5, pivotY = .5 }, 800, 600));
        stage.SetStage(new() { layers = [new() { id = "fixed", parallax = 0 }] });
        Reject(() => stage.FollowCamera("stage/fixed", new() { pivotX = .5, pivotY = .5 }, 800, 600));
    }
    internal static void ActualSessionPivotsRestoreAndTaskCancellation(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var edits = new StoryEditService(codec); var draft = FirstStoryTemplate.Create(codec);
        var show = edits.Add(draft, "opening", StoryNodeIds.stage, new StageNode { operation = StageOperation.ShowCharacter, resourceId = "hero", target = "hero",
            transform = new() { x = 300, y = 200, pivotX = 30, pivotY = 40, scaleX = 2, scaleY = 2 } }, new(0, 0));
        edits.Insert(draft, draft.graph.document.edges.Single(edge => edge.output.nodeId.value == "entry").id, show);
        var follow = edits.Add(draft, "opening", StoryNodeIds.stage, new StageNode { operation = StageOperation.FollowCamera, target = "character/hero", transform = new() { pivotX = .5, pivotY = .5 } }, new(0, 0));
        edits.Insert(draft, draft.graph.document.edges.Single(edge => edge.output.nodeId == show).id, follow);
        var image = new TextureAsset(1, 1, TextureColorSpace.Srgb, "png");
        var actor = new CharacterAsset { resourceId = "hero", textures = [image], poses = [new() { parts = [new() { id = "body", width = 100, height = 120, texture = 0 }] }], expressions = [new()] };
        var game = Game(draft, compiler, actor);
        using var session = new NovelSession(game); session.StartNew();
        var body = session.stage.BuildFrame(800, 600).items.Single();
        Check(session.story!.status == StoryStatus.Dialogue && Near(body.topLeft.X, 340) && Near(body.topLeft.Y, 220),
            "The actual narrative-to-stage request dropped character pivots or failed to follow the authored anchor.");
        var saved = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture()));
        session.stage.SetCamera(0, 0, 1, 0); session.Restore(saved);
        Check(session.stage.cameraFollowOwner == "character/hero" && Same(body, session.stage.BuildFrame(800, 600).items.Single()), "Actual session native restoration lost following.");
        session.Advance(); session.Advance(); session.stage.StopCameraFollow(); session.Choose("sea");
        Check(session.Rollback() && session.stage.cameraFollowOwner == "character/hero", "Rollback did not restore the exact follow boundary.");
        var beforeHide = session.stage.BuildFrame(800, 600).items.Single(); session.stage.HideCharacter("hero");
        Check(session.stage.cameraFollowOwner.Length == 0, "Hiding a followed character retained a dangling camera owner.");
        session.stage.ShowCharacter("hero", actor, "", "", new() { x = 300, y = 200, pivotX = 30, pivotY = 40, scaleX = 2, scaleY = 2 });
        Check(Same(beforeHide, session.stage.BuildFrame(800, 600).items.Single()), "Hiding the follow target did not freeze logical framing.");
        var broken = new StageNode { operation = StageOperation.FollowCamera, target = "hero", transform = new() { pivotX = .5, pivotY = .5 } };
        edits.SetSettings(draft, follow, broken);
        Check(compiler.Compile(draft.ToAsset()).diagnostics.Any(item => item.code == "VN_CAMERA_FOLLOW" && item.nodeId?.value == follow.value), "Invalid follow owners lost their source-addressed compiler diagnostic.");
        broken.target = "character/missing"; edits.SetSettings(draft, follow, broken);
        using (var failed = new NovelSession(Game(draft, compiler, actor)))
        { failed.StartNew(); Check(failed.story!.status == StoryStatus.Faulted && failed.story.error.Contains(follow.value, StringComparison.Ordinal), "A dynamically missing target did not identify its requesting node."); }
        broken.target = "character/hero"; edits.SetSettings(draft, follow, broken);
        var fork = edits.AddParallel(draft, "opening", new(0, 0)); var scope = codec.Read<ParallelNode>(draft.graph.document.FindNode(fork)!); scope.waitAny = true; edits.SetSettings(draft, fork, scope);
        var waits = draft.graph.document.edges.Where(edge => edge.output.nodeId == fork).Select(edge => edge.input.nodeId).ToArray();
        edits.Connect(draft, new(show, new("next")), fork); edits.Connect(draft, new(new(scope.joinId), new("next")), new("hello"));
        draft.graph.document.RemoveEdge(draft.graph.document.edges.Single(edge => edge.output.nodeId == follow).id);
        edits.Insert(draft, draft.graph.document.edges.Single(edge => edge.output.nodeId == fork && edge.input.nodeId == waits[0]).id, follow);
        edits.SetSettings(draft, waits[0], new WaitNode { seconds = 10 }); edits.SetSettings(draft, waits[1], new WaitNode { seconds = .1 });
        using var parallel = new NovelSession(Game(draft, compiler, actor)); parallel.StartNew();
        Check(parallel.story!.status == StoryStatus.Parallel && parallel.stage.cameraFollowOwner == "character/hero", "A parallel follow task failed before its wait.");
        parallel.Restore(native.Deserialize<NovelSessionState>(native.Serialize(parallel.Capture()))); parallel.Tick(.2);
        Check(parallel.story.status == StoryStatus.Dialogue && parallel.stage.cameraFollowOwner.Length == 0, "Wait-any cancellation after native restore retained a task-owned camera follow.");
    }
    private static NovelProjectAsset Game(
        StoryDraftState draft,
        StoryCompiler compiler,
        CharacterAsset actor
    ) {
        var chapter = draft.ToAsset(); var result = compiler.Compile(chapter);
        Check(result.success, string.Join("; ", result.diagnostics.Select(static item => item.message))); chapter.program = result.program;
        return new() { width = 800, height = 600, chapters = [chapter], startChapter = chapter.chapterId, characters = [actor] };
    }
    private static bool Near(
        double value,
        double expected
    ) => Math.Abs(value - expected) < .002;
    private static bool Same(
        StageDrawItem a,
        StageDrawItem b
    ) => Near(a.topLeft.X, b.topLeft.X) && Near(a.topLeft.Y, b.topLeft.Y) && Near(a.bottomRight.X, b.bottomRight.X) && Near(a.bottomRight.Y, b.bottomRight.Y);
    private static void Reject(Action action)
    { try { action(); } catch (Exception error) when (error is ArgumentException or InvalidOperationException) { return; } throw new InvalidOperationException("Invalid camera follow state was accepted."); }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}

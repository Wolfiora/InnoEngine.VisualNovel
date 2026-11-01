using System;
using System.Linq;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Stage.Composition;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class StageContracts
{
    internal static void GeometryAndAtomicRestore()
    {
        var scenery = new StageAsset { resourceId = "room", layers = [new() { id = "color", width = 10, height = 20,
            transform = new() { x = 100, y = 200, scaleX = -2, scaleY = 3 }, color = 0xAABBCCFF }] };
        var stage = new StageController([]); stage.SetStage(scenery);
        var first = stage.BuildFrame(1920, 1080).items.Single();
        Check(first.topLeft.X == 100 && first.topLeft.Y == 200 && first.bottomRight.X == 80 && first.bottomRight.Y == 260, "Affine reflection or scale was lost.");
        scenery.layers[0].transform.x = 999;
        Check(stage.BuildFrame(1920, 1080).items.Single().topLeft.X == 100, "Authored values changed an active stage.");
        StageState snapshot = stage.Capture(); snapshot.layers[0].textureId = Guid.NewGuid();
        bool failed = false;
        try { stage.Restore(snapshot); } catch (InvalidOperationException) { failed = true; }
        Check(failed && stage.BuildFrame(1920, 1080).items.Single().topLeft == first.topLeft, "Rejected stage restore changed the current composition.");
        stage.SetCamera(10, 0, 1, 0);
        Check(stage.BuildFrame(1920, 1080).items.Single().topLeft.X == 90, "Camera parallax was not evaluated.");
    }

    internal static void NarrativeCoordination(StoryNodeDataCodec codec, StoryCompiler compiler, StoryEditService edits)
    {
        var draft = FirstStoryTemplate.Create(codec);
        var request = edits.Add(draft, "opening", StoryNodeIds.stage, new StageNode { operation = StageOperation.SetStage, resourceId = "room" }, new(0, 300));
        edits.Insert(draft, draft.graph.document.edges.Single(edge => edge.output.nodeId.value == "entry").id, request);
        var chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var room = new StageAsset { resourceId = "room", layers = [new() { id = "sky", color = 0x112233FF }] };
        var project = new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId, stages = [room] };
        var session = new NovelSession(project); session.StartNew();
        Check(session.story!.status == StoryStatus.Dialogue && session.stage.hasContent, "Presentation requests were not coordinated before dialogue.");
        session.Advance(); session.Advance();
        session.stage.SetCamera(200, 0, 1, 0); session.Choose("sea");
        Check(session.Rollback() && session.stage.Capture().cameraX == 0, "Rollback did not restore the complete stage boundary.");
        var broken = new NovelSession(new() { chapters = [chapter], startChapter = chapter.chapterId }); broken.StartNew();
        Check(broken.story!.status == StoryStatus.Faulted && broken.story.error.Contains(request.value, StringComparison.Ordinal), "Missing stage resources lacked a source-addressed failure.");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

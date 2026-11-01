using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Authoring.TimelineEditing;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Stage.Composition;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class PerformanceContracts
{
    internal static void ReducedMotion()
    {
        PerformanceClipAsset clip = Clip();
        var stage = new StageController([], [clip]); stage.SetStage(Room()); stage.ApplyPerformance(Play()); stage.Tick(0, true);
        Check(!stage.performances.IsActive("move") && stage.BuildFrame(1920, 1080).items.Single().topLeft.X == 200, "Reduced motion did not complete an optional one-shot at its final pose.");
        clip.essential = true;
        var essential = new StageController([], [clip]); essential.SetStage(Room()); essential.ApplyPerformance(Play()); essential.Tick(0.25, true);
        Check(essential.performances.IsActive("move") && essential.BuildFrame(1920, 1080).items.Single().topLeft.X == 125, "Reduced motion skipped an explicitly essential performance.");
        clip.essential = false; clip.loop = true;
        var looping = new StageController([], [clip]); looping.SetStage(Room()); looping.ApplyPerformance(Play()); looping.Tick(10, true);
        Check(looping.BuildFrame(1920, 1080).items.Single().topLeft.X == 100, "Reduced motion animated a decorative loop.");
    }
    internal static void IndependentShake()
    {
        var move = Clip();
        var shake = new PerformanceClipAsset { resourceId = "shake", duration = 1, tracks = [new() { target = "actor", property = PerformanceProperty.ShakeX,
            keys = [new() { value = 0 }, new() { time = 1, value = 20 }] }] };
        var stage = new StageController([], [move, shake]); stage.SetStage(Room()); stage.ApplyPerformance(Play());
        stage.ApplyPerformance(new() { operation = PerformanceOperation.Play, instanceId = "shake", clipId = "shake", bindings = [new() { slot = "actor", target = "stage/panel" }] });
        stage.Tick(0.5);
        Check(stage.Capture().performances.Length == 2 && stage.BuildFrame(1920, 1080).items.Single().topLeft.X == 160, "Independent shake replaced the main motion writer or failed to add its offset.");
        shake.tracks[0].keys[1].value = double.MaxValue;
        bool rejected = false;
        try { _ = new Inno.VisualNovel.Stage.Timeline.PerformancePlayer(shake); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "A finite but unrenderable keyframe escaped validation.");
    }
    internal static void StageOwnership(SerializationRegistry native)
    {
        PerformanceClipAsset clip = Clip();
        var stage = new StageController([], [clip]); stage.SetStage(Room());
        stage.ApplyPerformance(Play()); stage.Tick(0.5);
        Check(stage.BuildFrame(1920, 1080).items.Single().topLeft.X == 150, "Bound position offsets were not applied about the authored owner pivot.");
        StageState snapshot = native.Deserialize<StageState>(native.Serialize(stage.Capture()));
        stage.Tick(0.5);
        Check(!stage.performances.IsActive("move") && stage.BuildFrame(1920, 1080).items.Single().topLeft.X == 200, "A completed performance did not hold its final pose.");
        stage.Restore(snapshot);
        Check(stage.performances.IsActive("move") && stage.BuildFrame(1920, 1080).items.Single().topLeft.X == 150, "Native stage restoration lost active performance state.");
        stage.ApplyPerformance(new() { operation = PerformanceOperation.Pause, instanceId = "move" }); stage.Tick(2);
        Check(stage.BuildFrame(1920, 1080).items.Single().topLeft.X == 150, "Paused performance time advanced.");
        stage.ApplyPerformance(new() { operation = PerformanceOperation.Seek, instanceId = "move", time = 0.25 });
        Check(stage.BuildFrame(1920, 1080).items.Single().topLeft.X == 125, "Pure seeking depended on previous sampled frames.");
        var replacement = Play(); replacement.instanceId = "replacement"; stage.ApplyPerformance(replacement);
        Check(stage.Capture().performances.Length == 1 && stage.Capture().performances[0].instanceId == "replacement", "Competing property writers were not explicitly replaced.");
        snapshot.performances[0].bindings[0].target = "missing";
        bool rejected = false;
        try { stage.Restore(snapshot); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && stage.Capture().performances[0].instanceId == "replacement", "Rejected performance restoration mutated the active stage.");
        var edit = Clip();
        bool collided = false;
        try { PerformanceEditService.MoveKey(edit, 0, 0, 1); } catch (InvalidOperationException) { collided = true; }
        Check(collided && edit.tracks[0].keys[0].time == 0, "Rejected key collision changed the clip.");
        PerformanceEditService.SetKey(edit, 0, 0.5, 80, PerformanceInterpolation.Hold);
        Check(edit.tracks[0].keys.Select(static key => key.time).SequenceEqual(new[] { 0d, 0.5, 1 }), "Key insertion did not retain strict chronological order.");
    }
    internal static void SessionWait(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        StoryEditService edits
    ) {
        var draft = FirstStoryTemplate.Create(codec);
        var play = edits.Add(draft, "opening", StoryNodeIds.performance, Play(), new(0, 300));
        var wait = edits.Add(draft, "opening", StoryNodeIds.performance, new PerformanceNode { operation = PerformanceOperation.Wait, instanceId = "move" }, new(0, 350));
        edits.Connect(draft, new(new("entry"), new("next")), play); edits.Connect(draft, new(play, new("next")), wait); edits.Connect(draft, new(wait, new("next")), new("hello"));
        var chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var clip = Clip(); var room = Room();
        var game = new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId, stages = [room], initialStage = room.resourceId, performances = [clip] };
        using var session = new NovelSession(game); session.StartNew(); session.Tick(0.25);
        Check(session.story!.status == StoryStatus.Presentation, "A performance wait did not suspend narrative progression.");
        var saved = session.Capture();
        clip.tracks[0].keys[1].value = 999; room.layers[0].transform.x = 999;
        session.Tick(1);
        Check(session.story.status == StoryStatus.Dialogue, "Completed performance did not release its narrative wait.");
        session.Restore(saved);
        Check(session.story.status == StoryStatus.Presentation && session.stage.BuildFrame(1920, 1080).items.Single().topLeft.X == 125, "A restored session retained mutable source clip or scenery aliases.");
        session.Tick(0.75);
        Check(session.story.status == StoryStatus.Dialogue && session.stage.BuildFrame(1920, 1080).items.Single().topLeft.X == 200, "Restored performance completion diverged.");
    }
    private static StageAsset Room() => new() { resourceId = "room", layers = [new() { id = "panel", width = 20, height = 20, transform = new() { x = 100 } }] };
    private static PerformanceClipAsset Clip() => new() { resourceId = "motion", duration = 1, tracks = [new() { target = "actor", property = PerformanceProperty.X,
        keys = [new() { value = 0 }, new() { time = 1, value = 100 }] }] };
    private static PerformanceNode Play() => new() { operation = PerformanceOperation.Play, instanceId = "move", clipId = "motion", bindings = [new() { slot = "actor", target = "stage/panel" }] };
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Authoring.TimelineEditing;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Stage.Timeline;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class PerformanceMarkerContracts
{
    internal static void WaitRestoreAndPolicy(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var draft = FirstStoryTemplate.Create(codec); var edits = new StoryEditService(codec);
        var play = edits.Add(draft, "opening", StoryNodeIds.performance, new PerformanceNode { operation = PerformanceOperation.Play, clipId = "timing", instanceId = "one" }, new(0, 100));
        edits.Insert(draft, draft.graph.document.edges.Single(static edge => edge.output.nodeId.value == "entry").id, play);
        var wait = edits.Add(draft, "opening", StoryNodeIds.performance, new PerformanceNode { operation = PerformanceOperation.WaitMarker, instanceId = "one", markerId = "middle" }, new(0, 200));
        edits.Insert(draft, draft.graph.document.edges.Single(edge => edge.output.nodeId == play).id, wait);
        var chapter = draft.ToAsset(); var compiled = compiler.Compile(chapter); Check(compiled.success, string.Join("; ", compiled.diagnostics.Select(static issue => issue.message))); chapter.program = compiled.program;
        var clip = new PerformanceClipAsset { resourceId = "timing", duration = 2, loop = true, markers = [new() { id = "middle", name = "同步", time = 1 }] };
        var game = new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId, performances = [clip] };
        using var session = new NovelSession(game); session.StartNew(); session.Tick(.4);
        Check(session.story!.status == StoryStatus.Presentation && session.story.pendingPerformance?.markerId == "middle", "A named wait did not suspend at its actual timeline boundary.");
        var snapshot = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture()));
        clip.markers[0].time = 1.9; session.Tick(.61);
        Check(session.story.status == StoryStatus.Dialogue, "Running timeline markers retained editable source aliases.");
        session.Restore(snapshot); Check(session.story!.status == StoryStatus.Presentation, "Native restore discarded the marker wait."); session.Tick(.61);
        Check(session.story.status == StoryStatus.Dialogue, "Native continuation failed to restore the active clip's first marker arrival.");
        using var reduced = new NovelSession(game); reduced.preferences.reducedMotion = true; reduced.StartNew(); reduced.Tick(0);
        Check(reduced.story!.status == StoryStatus.Dialogue, "A suppressed optional loop left its named marker wait stuck.");
        clip.essential = true;
        using var essential = new NovelSession(game); essential.preferences.reducedMotion = true; essential.StartNew(); essential.Tick(0);
        Check(essential.story!.status == StoryStatus.Presentation, "Reduced-motion preferences skipped an essential clip's marker.");
        var node = draft.graph.document.FindNode(wait)!; var invalid = codec.Read<PerformanceNode>(node); invalid.markerId = "unknown"; codec.Write(node, invalid);
        chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        using var missing = new NovelSession(new() { chapters = [chapter], startChapter = chapter.chapterId, performances = [clip] }); missing.StartNew();
        Check(missing.story!.status == StoryStatus.Faulted && missing.story.error.Contains(wait.value, StringComparison.Ordinal), "A missing marker did not identify its story source.");
    }
    internal static void MarkerEditsAndRangeCopies(SerializationRegistry native)
    {
        var clip = new PerformanceClipAsset { resourceId = "move", duration = 3, tracks = [new() { target = "camera", property = PerformanceProperty.X,
            keys = [new() { time = 0, value = 0 }, new() { time = .5, value = 10 }, new() { time = 1, value = 20 }] }] };
        PerformanceEditService.SetMarker(clip, new() { id = "middle", name = "中点", time = 1 });
        var restored = native.Deserialize<PerformanceMarker>(native.Serialize(clip.markers.Single()));
        Check(restored.id == "middle" && restored.time == 1 && Math.Abs(PerformanceEditService.Snap(.126, .05, 3) - .15) < 1e-12, "Native marker identity or explicit timeline snapping was lost.");
        var owner = new StagePerformanceController([clip]); owner.Apply(new() { operation = PerformanceOperation.Play, clipId = "move", instanceId = "one" }, ["camera"]);
        owner.Tick(.5); Check(!owner.HasReachedMarker("one", "middle"), "A marker arrived before its logical threshold.");
        owner.Apply(new() { operation = PerformanceOperation.Cancel, instanceId = "one" }, ["camera"]);
        Check(owner.HasReachedMarker("one", "middle"), "Cancelling an instance did not release a marker wait.");
        int count = PerformanceEditService.CopyRange(clip, [0], 0, .5, 2);
        Check(count == 2 && clip.tracks[0].keys.Length == 5 && clip.tracks[0].keys.Single(static key => key.time == 2.5).value == 10, "Time range copies moved original keys or lost values.");
        clip.tracks[0].keys.Single(static key => key.time == 2).curve.x1 = .8;
        Check(clip.tracks[0].keys[0].curve.x1 != .8, "A copied range retained mutable timing handles.");
        byte[] before = native.Serialize(clip.tracks[0]); bool rejected = false;
        try { PerformanceEditService.CopyRange(clip, [0], 0, .5, .5); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && native.Serialize(clip.tracks[0]).SequenceEqual(before), "An unapproved key collision partially modified the clip.");
        PerformanceEditService.CopyRange(clip, [0], 0, .5, .5, true);
        Check(new PerformancePlayer(clip).Evaluate(1).Single().value == 10 && clip.markers.Single().id == "middle", "Explicit replacement changed unrelated markers or failed exact key values.");
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}

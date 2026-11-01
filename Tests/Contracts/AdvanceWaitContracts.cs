using System;
using System.Linq;
using Inno.Core.Graphs;
using Inno.Core.Serialization;
using Inno.Text;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.Preview;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Shell;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class AdvanceWaitContracts
{
    internal static void ActualPagesSavingAndSingleConsumption(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var fixture = Create(codec);
        foreach (string family in ShellThemeTemplates.names)
        {
            var game = Game(fixture.draft, compiler, family);
            using var session = new NovelSession(game); session.StartNew(); session.Advance();
            if (family == "Paper") session.ToggleSkipping(); else session.ToggleAutomatic();
            session.Advance();
            Check(session.story!.status == StoryStatus.AdvanceWait && !session.automatic && !session.skipping && session.history.Count == 1,
                family + " input wait failed to stop automatic reading or created a fake history line.");
            using var view = new NovelGameView(game, session, Measure);
            Check(view.BuildFrame().items.Any(static item => item.id == "advance-wait-hint") && view.BuildFrame().items.All(static item => item.id != "dialogue"),
                family + " wait UI displayed dialogue or omitted its editable continuation hint.");
            view.HandleInput(new() { skipHeld = true }); session.Tick(30);
            Check(session.story.status == StoryStatus.AdvanceWait && !session.skipping, family + " elapsed time or held skipping bypassed explicit input.");
            var saved = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture()));
            view.HandleInput(new() { confirm = true });
            Check(session.interactiveStory?.status == StoryStatus.Choice && session.interactiveStory.choices.Count == 2,
                family + " wait confirmation also chose the following option.");
            Check(session.Rollback() && session.story!.status == StoryStatus.AdvanceWait, family + " rollback did not recover the explicit input boundary.");
            session.Restore(saved);
            saved.narrative.status = StoryStatus.Timer;
            Reject(() => session.Restore(saved));
            Check(session.story!.status == StoryStatus.AdvanceWait, "A forged timed status replaced an active input-wait session.");
        }
    }

    internal static void ParallelScopeAndNativeTasks(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var edits = new StoryEditService(codec); var draft = FirstStoryTemplate.Create(codec);
        var groups = new ParallelEditService(edits, codec); GraphNodeId fork = groups.Create(draft, "opening", new(0, 200));
        var definition = codec.Read<ParallelNode>(draft.graph.document.FindNode(fork)!);
        GraphNodeId[] waits = definition.branches.Select(branch => draft.graph.document.edges.Single(edge => edge.output.nodeId == fork && edge.output.portId.value == branch).input.nodeId).ToArray();
        edits.SetSettings(draft, waits[0], new WaitNode { waitForInput = true, seconds = 0 });
        edits.SetSettings(draft, waits[1], new WaitNode { seconds = 1 });
        edits.Connect(draft, new(new("entry"), new("next")), fork); edits.Connect(draft, new(new(definition.joinId), new("next")), new("hello"));
        using var session = new NovelSession(Game(draft, compiler)); session.StartNew(); session.Tick(.5);
        Check(session.story!.status == StoryStatus.Parallel && session.interactiveStory?.status == StoryStatus.AdvanceWait,
            "The bounded task group did not assign its input wait to the single interactive owner.");
        var saved = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture())); session.Restore(saved);
        Check(Math.Abs(saved.narrative.tasks[1].execution.waitElapsed - .5) < .000001 && session.Advance() && session.story!.status == StoryStatus.Parallel,
            "Restoring or advancing the input owner changed its sibling timer.");
        session.Tick(.6);
        Check(session.interactiveStory?.status == StoryStatus.Dialogue && !session.interactiveStory.isTextComplete, "The released task input leaked into the post-join dialogue.");
        edits.SetSettings(draft, waits[1], new WaitNode { waitForInput = true });
        Check(!compiler.Compile(draft.ToAsset()).success, "Parallel analysis allowed two independent advance-input owners.");
    }

    internal static void LocalAndRecordedRouteTargets(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var fixture = Create(codec); var game = Game(fixture.draft, compiler);
        using var route = new NovelPreviewSession(game, fixture.draft, compiler, new() { mode = PreviewRunMode.Route, nodeId = fixture.gate.value }, Measure);
        route.Tick(0);
        Check(route.targetReached && route.session.interactiveStory?.status == StoryStatus.AdvanceWait, "Route traversal did not stop before advancing its exact wait target.");
        using var replay = new NovelPreviewSession(game, fixture.draft, compiler, route.CaptureRoute(), Measure);
        replay.Tick(0); Check(replay.targetReached && replay.error.Length == 0, "Recording lost an advance-wait target or invented an ordered decision.");
        replay.ContinuePlaying(); replay.HandleInput(new() { confirm = true });
        Check(replay.session.interactiveStory?.status == StoryStatus.Choice, "Manual continuation did not release exactly the recorded wait boundary.");
        using var local = new NovelPreviewSession(game, fixture.draft, compiler, new() { mode = PreviewRunMode.Local, nodeId = fixture.gate.value }, Measure);
        Check(local.session.interactiveStory?.status == StoryStatus.AdvanceWait && local.session.history.Count == 0, "Local preview guessed prior dialogue or bypassed an input wait.");
    }

    private static (StoryDraftState draft, GraphNodeId gate) Create(StoryNodeDataCodec codec)
    {
        var draft = FirstStoryTemplate.Create(codec); var edits = new StoryEditService(codec);
        GraphNodeId gate = edits.Add(draft, "opening", StoryNodeIds.wait, new WaitNode { waitForInput = true, seconds = 0 }, new(400, 200));
        edits.Connect(draft, new(new("hello"), new("next")), gate); edits.Connect(draft, new(gate, new("next")), new("choice"));
        return (draft, gate);
    }
    private static NovelProjectAsset Game(
        StoryDraftState draft,
        StoryCompiler compiler,
        string family = "Clear"
    ) {
        var chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program ?? throw new InvalidOperationException("Advance wait fixture failed compilation.");
        var theme = ShellThemeTemplates.Create(family, new FontAsset());
        return new() { chapters = [chapter], startChapter = chapter.chapterId, screens = theme.screens, themes = [theme], defaultTheme = theme.resourceId };
    }
    private static (float width, float height) Measure(
        UiElementDefinition element,
        string text
    ) => (text.Length * 12, element.style.fontSize);
    private static void Reject(Action action)
    { try { action(); } catch (InvalidOperationException) { return; } throw new InvalidOperationException("An invalid wait restoration was accepted."); }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}

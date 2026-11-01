using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.Text;
using Inno.VisualNovel.Authoring.Preview;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.UI.Input;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class InteractionContracts
{
    internal static void SelectedMarks(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var draft = FirstStoryTemplate.Create(codec); var edits = new StoryEditService(codec);
        edits.Connect(draft, new(new("sea"), new("next")), new("choice"));
        var source = draft.ToAsset(); source.program = compiler.Compile(source).program!;
        using var session = new NovelSession(new() { chapters = [source], startChapter = source.chapterId }); session.StartNew(); session.Advance(); session.Advance();
        session.Choose("sea"); session.Advance(); session.Advance();
        Check(session.story!.choices.Single(static option => option.id == "sea").selected && !session.story.choices.Single(static option => option.id == "forest").selected,
            "A loop lost its selected option mark or marked an unchosen option.");
        var state = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture())); session.Restore(state);
        Check(session.story!.choices.Single(static option => option.id == "sea").selected, "Native restoration lost selected option state.");
        var invalid = native.Deserialize<NovelSessionState>(native.Serialize(state)); invalid.narrative.selectedChoices[0].optionId = "missing"; var active = session.story;
        bool rejected = false; try { session.Restore(invalid); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && session.story == active, "An invalid option mark replaced the active game.");
        session.Choose("forest"); Check(session.Rollback(), "Chosen future did not create a rollback boundary.");
        Check(session.story!.choices.Single(static option => option.id == "sea").selected && !session.story.choices.Single(static option => option.id == "forest").selected,
            "Rollback kept selected marks from an abandoned future branch.");
    }

    internal static void RecordedLoopAndInputOwnership(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var draft = FirstStoryTemplate.Create(codec); var edits = new StoryEditService(codec);
        edits.Connect(draft, new(new("choice"), new("sea")), new("choice"));
        var source = draft.ToAsset(); source.program = compiler.Compile(source).program!;
        var game = new NovelProjectAsset { chapters = [source], startChapter = source.chapterId,
            screens = new[] { "title", "reading", "pause", "settings", "history", "credits" }.Select(role => new NovelScreenBinding { role = role, screen = ShellScreenTemplates.Create(role, new FontAsset(), "Clear") }).ToArray() };
        using var owner = new NovelPreviewSession(game, draft, compiler, new(), static (_, text) => (text.Length * 10, 40));
        owner.session.Advance(); owner.session.Advance(); long before = owner.session.story!.interactionSequence;
        var frame = owner.view.BuildFrame(); var option = frame.items.First(static item => item.kind == Model.Interface.UiElementKind.Button && item.id.EndsWith("/option", StringComparison.Ordinal));
        float x = option.bounds.x + 20, y = option.bounds.y + 20;
        owner.HandleInput(new() { pointerInside = true, pressed = true, x = x, y = y });
        owner.HandleInput(new() { pointerInside = true, released = true, confirm = true, x = x, y = y });
        Check(owner.session.story.interactionSequence == before + 1 && owner.session.story.lastInteractionResult?.sequence == 1,
            "One pointer and keyboard sample activated a choice twice or skipped the next interaction.");
        Check(owner.view.BuildFrame().items.Count(static item => item.text == "✓") == 1, "Editable choice templates did not bind the actual selected mark.");
        var recorded = owner.CaptureRoute();
        Check(recorded.choices.SequenceEqual(new[] { "sea" }) && recorded.nodeId == "choice" && recorded.targetInteraction == before + 1,
            "Actual route recording lost a loop occurrence or retained duplicate UI activations.");
        using var replay = new NovelPreviewSession(game, draft, compiler, recorded, static (_, _) => (100, 40)); replay.Tick(0);
        Check(replay.targetReached && replay.waitingAtTarget && replay.session.story!.interactionSequence == recorded.targetInteraction,
            "Recorded loop stopped at the target's first occurrence instead of its recorded visit.");
        replay.ContinuePlaying(); replay.session.Choose("forest"); replay.ObserveInteraction(); replay.Tick(0.1);
        Check(!replay.waitingAtTarget && replay.session.story!.visibleGraphemes > 0, "Continuing from a route target kept ordinary game time frozen.");
        replay.session.story!.SetVariable("player", Model.Values.NovelValue.String("调试修改")); replay.InvalidateRouteRecording("changed initial state");
        bool denied = false; try { replay.CaptureRoute(); } catch (InvalidOperationException) { denied = true; }
        Check(denied, "A debugger-modified route was presented as reproducible from its original preconditions.");
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}

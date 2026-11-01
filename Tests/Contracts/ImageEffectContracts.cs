using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Rendering.Frames;
using Inno.VisualNovel.Stage.Composition;
using Inno.VisualNovel.UI.Documents;
using Inno.VisualNovel.Model.Interface;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class ImageEffectContracts
{
    internal static void ClocksAndRestore(SerializationRegistry native)
    {
        var screen = Screen(); var transition = Transition();
        var stage = new StageController([], screenPresets: [screen], transitionPresets: [transition]);
        stage.ApplyScreenEffect(new() { operation = ScreenEffectOperation.Fade, instanceId = "fade", resourceId = screen.resourceId, duration = 2 });
        stage.Tick(.5); stage.ApplyScreenEffect(new() { operation = ScreenEffectOperation.Pause, instanceId = "fade" }); stage.Tick(3);
        Check(stage.BuildFrame(1920, 1080).effects!.Single().strength == .25, "Paused screen fade consumed logical time.");
        stage.ApplyTransition(new() { resourceId = transition.resourceId }); stage.Tick(.25);
        var snapshot = native.Deserialize<StageState>(native.Serialize(stage.Capture()));
        var candidate = stage.Fork(snapshot);
        Check(candidate.BuildFrame(1920, 1080).transition!.progress == .25 && candidate.BuildFrame(1920, 1080).effects!.Single().strength == .25, "Native effect restoration lost independent clocks.");
        candidate.ApplyTransition(new() { operation = TransitionOperation.Cancel });
        Check(!candidate.transitions.active && candidate.screenEffects.IsActive("fade"), "Cancel did not restore the previous declarative stage.");
        snapshot.transitions[0].elapsed = double.NaN;
        bool rejected = false; try { stage.Restore(snapshot); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && stage.transitions.active, "Invalid transition state replaced the live boundary.");
        stage.Tick(0, true); Check(!stage.transitions.active && !stage.screenEffects.IsActive("fade"), "Reduced motion left an unfinishable wait.");
        screen.screen.kind = ScreenEffectKind.Flash;
        var flash = new StageController([], screenPresets: [screen]); flash.ApplyScreenEffect(new() { resourceId = screen.resourceId }); flash.Tick(0, allowFlashing: false);
        Check(flash.BuildFrame(1920, 1080).effects!.Count == 0, "Flash policy did not suppress a full-frame flash.");
    }

    internal static void SessionSnapshots(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        StoryEditService edits,
        SerializationRegistry native
    ) {
        var draft = FirstStoryTemplate.Create(codec);
        var transition = edits.Add(draft, "opening", StoryNodeIds.transition, new TransitionNode { resourceId = "dissolve", scope = TransitionScope.Output, waitForCompletion = true }, new(0, 0));
        edits.Connect(draft, new(new("entry"), new("next")), transition); edits.Connect(draft, new(transition, new("next")), new("hello"));
        var chapter = draft.ToAsset(); var result = compiler.Compile(chapter);
        Check(result.success && result.program!.instructions.Count(item => item.sourceNodeId == transition.value) == 2, "Authored wait was not lowered with the original source identity."); chapter.program = result.program;
        using var session = new NovelSession(new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId, transitions = [Transition()] });
        session.PublishUiFrame(Page()); session.StartNew(); session.Tick(.25);
        Check(session.story!.status == StoryStatus.Presentation && session.transitionUi!.items.Count == 2, "Output boundary failed to freeze its previously displayed UI.");
        var save = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture()));
        session.Tick(1); Check(session.story.status == StoryStatus.Dialogue && session.transitionUi is null, "Completed transition retained its previous UI.");
        session.Restore(save); Check(session.stage.transitions.active && session.transitionUi!.items.Count == 2, "Native game restoration lost the output image boundary.");
        save.transitionUi.sequence++; bool rejected = false;
        try { session.Restore(save); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && session.stage.transitions.active && session.story.status == StoryStatus.Presentation, "Mismatched UI boundary replaced the current game.");
        session.Tick(.75); Check(session.story.status == StoryStatus.Dialogue, "Restored transition did not release its explicit wait.");
        save.transitionUi.sequence--; session.preferences.reducedMotion = true; session.Restore(save); session.Tick(0);
        Check(!session.stage.transitions.active && session.transitionUi is null && session.story.status == StoryStatus.Dialogue, "Reduced-motion loading rejected or retained a completed output boundary.");
    }

    internal static void ImagePlans()
    {
        var stage = new StageController([], screenPresets: [Screen()], transitionPresets: [Transition()]);
        stage.ApplyScreenEffect(new() { resourceId = "blur", includeUi = true });
        stage.ApplyTransition(new() { resourceId = "dissolve", scope = TransitionScope.Output });
        var composer = new NovelFrameComposer(); var material = new Inno.Rendering.Assets.MaterialAsset();
        var frame = composer.Compose(Page(), material, stage.BuildFrame(1920, 1080), effectsMaterial: material, previousUi: Page());
        Check(frame.image is NovelProcessImage { config.X: 101 }, "Output transition is not the final two-image operation.");
        var root = (NovelProcessImage)frame.image!;
        Check(root.source is NovelProcessImage { parameters.W: 1, source: NovelProcessImage { parameters.Y: 1 } }, "Blur did not use two independent axes with a final original-image blend.");
        Check(frame.quads[0].color == 0x102030FF && frame.quads[^1].color == 0xAABBCCFF, "UI underlays were painted over the stage or overlays.");
        Check(frame.image!.Paints().Count() >= 4, "Previous UI paint resources were omitted from the image dependency closure.");
        bool rejected = false; try { composer.Compose(Page(), material, stage.BuildFrame(1920, 1080), effectsMaterial: material); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Output transition silently substituted current UI for a missing previous image.");
        var saved = UiFrameSnapshot.Capture(Page()); saved.items[0].geometry[0] = double.NaN;
        rejected = false; try { saved.Restore(_ => throw new Exception(), _ => throw new Exception()); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Invalid UI image geometry reached the render composer.");
    }

    internal static VisualEffectAsset Screen() => new() { resourceId = "blur", kind = VisualEffectKind.Screen, screen = new() { kind = ScreenEffectKind.Blur } };
    internal static VisualEffectAsset Transition() => new() { resourceId = "dissolve", kind = VisualEffectKind.Transition, transition = new() { duration = 1, kind = TransitionKind.Dissolve } };
    private static UiFrame Page() => new(1920, 1080, [
        new("underlay", UiElementKind.Panel, new(0, 0, 1920, 1080), new(0, 0, 1920, 1080), 0x102030FF, 0xFFFFFFFF, "", null, 32, 0, null, false, 0xFFFFFFFF, behindStage: true),
        new("overlay", UiElementKind.Panel, new(10, 10, 100, 100), new(0, 0, 1920, 1080), 0xAABBCCFF, 0xFFFFFFFF, "", null, 32, 0, null, false, 0xFFFFFFFF)
    ]);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

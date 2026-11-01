using System;
using System.Collections.Generic;
using System.Linq;
using Inno.Core.Serialization;
using Inno.Core.Mathematics;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Interface.Animation;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Rendering.Frames;
using Inno.VisualNovel.UI.Animation;
using Inno.VisualNovel.UI.Binding;
using Inno.VisualNovel.UI.Documents;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Shell;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class UiAnimationContracts
{
    internal static void VisibilityInputAndRendering(SerializationRegistry native)
    {
        var button = Button();
        button.animation.enter = Motion(1, new() { opacity = 0, x = 100, scale = .5f });
        button.animation.exit = Motion(.4, new() { opacity = 0, y = 80 });
        button.visibleBinding = "visible";
        var page = Page(button); var data = new Data();
        var document = new UiDocumentInstance(page, Measure);
        UiDrawItem start = Item(document, data);
        Check(start.bounds == new Rect(20, 30, 100, 60) && start.opacity == 0 && Visual(start).x == 120 && Visual(start).width == 50,
            $"Entry modified layout or ignored the authored pose: {start.bounds}, opacity {start.opacity}, visual {Visual(start)}.");
        Check(document.HandleInput(new() { pointerInside = true, pressed = true, x = 130, y = 40 }, data).actions.Count == 0,
            "An invisible entry acquired pointer input.");
        for (int index = 0; index < 20; index++) document.BuildFrame(data);
        Check(Item(document, data).opacity == 0, "Rendering advanced UI animation time.");
        document.Tick(.5); UiDrawItem middle = Item(document, data);
        Near(middle.opacity, .5); Near(Visual(middle).x, 70); Near(Visual(middle).width, 75);
        Check(middle.fontSize == start.fontSize && middle.textPadding == start.textPadding, "Motion changed text shaping inputs.");
        document.HandleInput(new() { pointerInside = true, pressed = true, x = 80, y = 40 }, data);
        Check(document.HandleInput(new() { pointerInside = true, released = true, x = 80, y = 40 }, data).actions.Single().action == "test",
            "Pointer input did not follow the current scaled and translated element.");
        var painted = new NovelFrameComposer().Compose(new(400, 240, [middle]), null!);
        Check(painted.quads.Count > 0 && painted.quads[0].bounds == Visual(middle) && (painted.quads[0].color & 255) == 128,
            "The actual quad composer did not transform complete control geometry or opacity.");
        var captured = native.Deserialize<UiFrameSnapshot>(native.Serialize(UiFrameSnapshot.Capture(new(400, 240, [middle]))));
        var restored = captured.Restore(_ => throw new InvalidOperationException(), _ => throw new InvalidOperationException()).items.Single();
        Check(restored.presentation == middle.presentation && restored.opacity == middle.opacity, "Native frozen UI dropped presentation state.");
        document.Tick(.5); _ = Item(document, data); data.visible = false;
        Check(Item(document, data).opacity == 1, "Conditional hide disappeared before its exit interval.");
        Check(!document.HandleInput(new() { pointerInside = true, pressed = true, released = true, x = 30, y = 40 }, data).actions.Any(), "Exiting UI retained input ownership.");
        document.Tick(.2); Near(Item(document, data).opacity, .5);
        data.visible = true; UiDrawItem reversed = Item(document, data); Near(reversed.opacity, .5);
        document.Tick(1); Near(Item(document, data).opacity, 1);
        data.visible = false; _ = Item(document, data); document.Tick(.5);
        Check(!document.BuildFrame(data).items.Any(static item => item.id == "button"), "Finished conditional exits remained in layout.");
        data.visible = true; Near(Item(document, data).opacity, 0);
        document.SetReducedMotion(true); Near(Item(document, data).opacity, 1);
        data.visible = false; Check(!document.BuildFrame(data).items.Any(static item => item.id == "button"), "Reduced motion retained an exit wait.");
        var neverVisible = new UiDocumentInstance(page, Measure); Check(!neverVisible.BuildFrame(data).items.Any(static item => item.id == "button"), "Initially hidden controls played an exit.");
        Reject(() => document.Tick(double.NaN)); Reject(() => document.Tick(-1));
    }

    internal static void PurePreviewStatesAndFrozenExit(SerializationRegistry native)
    {
        var child = Button(); child.layout.x = 10; child.layout.y = 10;
        var parent = new UiElementDefinition { id = "panel", kind = UiElementKind.Panel, blocksPointer = false,
            layout = new() { x = 20, y = 20, width = 180, height = 100, clip = true }, children = [child] };
        parent.animation.enter = Motion(1, new() { x = 100, opacity = .2f });
        parent.animation.exit = Motion(1, new() { x = 40, opacity = 0 });
        child.animation.hover = Motion(1, new() { x = 30 });
        child.animation.focus = Motion(1, new() { y = 20 });
        child.animation.pressed = Motion(1, new() { scale = .8f });
        child.animation.waiting = Motion(1, new() { opacity = .4f }); child.animation.waitingBinding = "waiting";
        var data = new Data(); var page = Page(parent);
        var document = new UiDocumentInstance(page, Measure); _ = document.BuildFrame(data); document.Tick(1);
        var ordinary = Item(document, data);
        var preview = document.PreviewAnimation(data, "panel", UiAnimationEvent.Enter, .5).items.Single(static item => item.id == "button");
        Near(Visual(preview).x, 80); Near(preview.opacity, .6);
        Check(Item(document, data).presentation == ordinary.presentation, "Seeking a designer event changed the live animation clock.");
        document.HandleInput(new() { pointerInside = true, x = 40, y = 60 }, data);
        document.Tick(.5); var hovered = Item(document, data); Near(Visual(hovered).x, 45); Near(Visual(hovered).y, 50);
        data.waiting = true; _ = Item(document, data); document.Tick(.5); Near(Item(document, data).opacity, .7);
        document.Tick(1); Near(Item(document, data).opacity, .7);
        document.SetReducedMotion(true); var reduced = Item(document, data); Near(Visual(reduced).x, 30); Near(reduced.opacity, 1);
        document.SetReducedMotion(false); data.waiting = false; _ = document.BuildFrame(data);
        var outgoing = document.BeginExit(); data.rejectReads = true;
        Check(!outgoing.complete, "An authored page exit retired immediately.");
        outgoing.Tick(.5); var exit = outgoing.BuildFrame().items.Single(static item => item.id == "button");
        Near(Visual(exit).x, 50); Near(exit.opacity, .5);
        var composite = outgoing.Composite(new(800, 480, []));
        var scaled = composite.items.Single(static item => item.id == "outgoing/button"); Near(Visual(scaled).x, 100);
        outgoing.Tick(.5); Check(outgoing.complete && outgoing.BuildFrame().items.Count == 0 && data.writes == 0, "Frozen exits queried old bindings, performed writes or failed to retire.");

        data = new(); page = Page(Button()); page.root.children[0].animation.waitingBinding = "page.waiting";
        page.stateVariables = [new() { id = "waiting", initialValue = NovelValue.Boolean(true), scope = VariableScope.Local }];
        string duplicated = ScreenEditService.Duplicate(page, "button");
        ScreenEditService.Find(page, duplicated).animation.enter.enabled = true;
        Check(!page.root.children[0].animation.enter.enabled, "Duplicate controls share mutable animations.");
        var widget = UiWidgetEditService.Extract(page, "button", "Waiting component"); var target = Page();
        string inserted = UiWidgetEditService.Insert(target, "root", widget);
        Check(target.stateVariables.Length == 1 && ScreenEditService.Find(target, inserted).animation.waitingBinding == "page." + target.stateVariables[0].id,
            "Component insertion failed to retain and remap animation-only state.");
        Reject(() => UiStateEditService.Remove(target, target.stateVariables[0].id));
        UiStateEditService.Remove(target, target.stateVariables[0].id, true);
        Check(ScreenEditService.Find(target, inserted).animation.waitingBinding == "", "Explicit state removal left an animation-only binding.");
        var roundtrip = native.Deserialize<UiElementDefinition>(native.Serialize(parent));
        Check(roundtrip.animation.enter.pose.x == 100 && roundtrip.children[0].animation.waitingBinding == "waiting", "Native recursive serialization lost animation declarations.");
        var copy = UiDefinitionCopy.Copy(Page(parent)); copy.root.children[0].animation.exit.pose.x = 999;
        Check(parent.animation.exit.pose.x == 40, "A session shares mutable animation declarations with source.");
        page = Page(Button()); page.root.children[0].animation.enter.pose.scale = float.NaN; Reject(() => new UiDocumentInstance(page, Measure));

        parent.animation.enter.enabled = false; parent.visibleBinding = "visible"; parent.children[0].animation = new();
        data = new(); document = new(Page(parent), Measure); _ = document.BuildFrame(data); data.visible = false;
        Check(document.BuildFrame(data).items.Any(static item => item.id == "button"), "Hiding a parent discarded its ordinary child before the parent's exit.");
        document.Tick(.5); Near(Item(document, data).opacity, .5);
        Check(!document.HandleInput(new() { confirm = true }, data).actions.Any(), "A parent exit retained a child's keyboard focus.");
        document.Tick(.5); Check(!document.BuildFrame(data).items.Any(static item => item.id == "button"), "A finished parent exit retained its child layout.");

        child = Button(); child.animation.waiting = Motion(1, new() { opacity = .4f });
        page = Page(child); page.root.kind = UiElementKind.Vertical; page.root.repeatBinding = "rows";
        data = new() { rows = [new("a", new Dictionary<string, NovelValue>()), new("b", new Dictionary<string, NovelValue>())] };
        document = new(page, Measure); _ = document.BuildFrame(data); document.Tick(.5);
        var beforeRows = document.BuildFrame(data).items.Where(static item => item.id != "root").ToDictionary(static item => item.id, static item => item.opacity);
        data.rows = [data.rows[1], data.rows[0]];
        Check(document.BuildFrame(data).items.Where(static item => item.id != "root").All(item => Math.Abs(item.opacity - beforeRows[item.id]) < .001),
            "Reordering stable repeated rows restarted their animation clocks.");
    }

    internal static void ActualGameNavigation(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var chapter = FirstStoryTemplate.Create(codec).ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var game = new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId,
            screens = new[] { "title", "reading", "pause", "settings", "history", "credits" }.Select(role => new NovelScreenBinding
            { role = role, screen = ShellScreenTemplates.Create(role, null!, "Clear") }).ToArray() };
        var title = game.screens.Single(static item => item.role == "title").screen!;
        title.root.animation.exit = Motion(.5, new() { opacity = 0, y = 10 });
        var reading = game.screens.Single(static item => item.role == "reading").screen!;
        reading.root.animation.enter = Motion(.5, new() { opacity = 0 });
        using var session = new NovelSession(game);
        using var view = new NovelGameView(game, session, Measure);
        view.BuildFrame(); view.HandleInput(new() { confirm = true });
        var first = view.BuildFrame();
        Check(session.story!.nodeId == "hello" && !session.story.isTextComplete && first.items.Any(static item => item.id.StartsWith("outgoing/", StringComparison.Ordinal)),
            $"Navigation discarded an authored exit or advanced new reading input: node {session.story.nodeId}, complete {session.story.isTextComplete}, outgoing {first.items.Count(static item => item.id.StartsWith("outgoing/", StringComparison.Ordinal))}.");
        Check(first.items.Single(static item => item.id == "root").opacity == 0, "The actual new page failed to enter from its authored pose.");
        view.Tick(.25); var middle = view.BuildFrame(); Near(middle.items.Single(static item => item.id == "root").opacity, .5);
        Near(middle.items.Single(static item => item.id == "outgoing/root").opacity, .5);
        view.HandleInput(new() { cancel = true }); var menu = view.BuildFrame();
        Check(session.page == "pause" && !menu.items.Any(static item => item.id.StartsWith("outgoing/outgoing/", StringComparison.Ordinal)),
            "Rapid navigation accumulated old input owners or recursively nested frozen exits.");
        view.HandleInput(new() { cancel = true }); view.Tick(.6); var resumed = view.BuildFrame();
        Check(session.page == "reading" && !resumed.items.Any(static item => item.id.StartsWith("outgoing/", StringComparison.Ordinal)), "Completed exits survived navigation return.");
        var saved = session.Capture(); session.Restore(saved); var loaded = view.BuildFrame();
        Check(!loaded.items.Any(static item => item.id.StartsWith("outgoing/", StringComparison.Ordinal)) && loaded.items.Single(static item => item.id == "root").opacity == 0,
            "Loading a session retained transient page animation ownership.");
        Check(view.CaptureReadingFrame().items.Single(static item => item.id == "root").opacity == 1 && view.BuildFrame().items.Single(static item => item.id == "root").opacity == 0,
            "A save image contained transient entry motion or changed the live presentation.");
        session.preferences.reducedMotion = true; Near(view.BuildFrame().items.Single(static item => item.id == "root").opacity, 1);
    }

    private static UiMotionDefinition Motion(
        double duration,
        UiMotionPose pose
    ) => new() { enabled = true, duration = duration, easing = UiMotionEasing.Linear, pose = pose };
    private static UiElementDefinition Button() => new() { id = "button", kind = UiElementKind.Button, action = "test", text = "Text",
        layout = new() { x = 20, y = 30, width = 100, height = 60 }, style = new() { background = 0xFFFFFFFF, focus = 0 } };
    private static ScreenAsset Page(params UiElementDefinition[] children) => new() { width = 400, height = 240,
        root = new() { id = "root", kind = UiElementKind.Panel, blocksPointer = false, layout = new() { width = 400, height = 240 }, children = children } };
    private static (float width, float height) Measure(UiElementDefinition element, string text) => (text.Length * 8, 20);
    private static UiDrawItem Item(UiDocumentInstance document, Data data) => document.BuildFrame(data).items.Single(static item => item.id == "button");
    private static Rect Visual(UiDrawItem item) => UiMotionSampling.Bounds(item.bounds, item.presentation ?? Matrix.identity);
    private static void Near(double actual, double expected) => Check(Math.Abs(actual - expected) < .001, $"UI motion expected {expected}, got {actual}.");
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Reject(Action action) { try { action(); } catch (ArgumentException) { return; } catch (InvalidOperationException) { return; } throw new InvalidOperationException("Invalid UI animation was accepted."); }
    private sealed class Data : UiDataSource
    {
        internal bool visible = true, waiting, rejectReads;
        internal int writes;
        internal UiDataRow[] rows = [];
        public override bool TryRead(string path, out NovelValue value)
        {
            if (rejectReads) throw new InvalidOperationException("An outgoing page read retired data.");
            value = NovelValue.Boolean(path == "visible" ? visible : waiting); return true;
        }
        public override bool TryWrite(string path, NovelValue value) { writes++; return true; }
        public override IReadOnlyList<UiDataRow> ReadRows(string path) => rows;
    }
}

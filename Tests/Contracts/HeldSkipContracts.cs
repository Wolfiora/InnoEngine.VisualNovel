using System;
using System.Linq;
using Inno.Core.Input;
using Inno.Core.Serialization;
using Inno.Rendering;
using Inno.Rendering.Runtime;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Integration.Services;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Interface;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Shell;
using Inno.VisualNovel.UI.Input;
using Inno.VisualNovel.UI.Layout;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class HeldSkipContracts
{
    internal static void ReadingEdgesAndBarriers(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var game = Game(codec, compiler);
        using var session = new NovelSession(game); session.StartNew();
        session.SetSkippingHeld(true, true); session.Tick(.001);
        Check(!session.skipping && session.story!.nodeId == "hello", "Held skip crossed an unread dialogue.");
        session.Tick(20); session.SetSkippingHeld(true, true); session.Tick(0);
        Check(!session.skipping && session.story!.nodeId == "hello", "A continuously held input rearmed after the unread stop.");
        session.SetSkippingHeld(false, true); session.SetSkippingHeld(true, true); session.Tick(0);
        Check(session.story!.status == StoryStatus.Choice && !session.skipping, "Held skip did not stop at the decision boundary.");
        session.SetSkippingHeld(true, true); session.Choose("sea"); session.SetSkippingHeld(true, true);
        Check(session.story.nodeId == "sea" && !session.skipping && !session.story.isTextComplete, "Choice confirmation rearmed the still-held input or revealed the next line.");
        session.preferences.skipReadOnly = false;
        session.SetSkippingHeld(false, true); session.SetSkippingHeld(true, true);
        Check(session.skipping, "A new hold edge did not respect the player's skip-all policy.");
        var state = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture())); session.Restore(state);
        session.SetSkippingHeld(true, true); Check(!session.skipping, "Restoring a save restarted a held transient input.");
        session.SetSkippingHeld(false, true); session.SetSkippingHeld(true, true); session.OpenPage("pause"); session.SetSkippingHeld(true, false);
        session.OpenPage("reading"); session.SetSkippingHeld(true, true);
        Check(!session.skipping, "Closing a menu rearmed a held shortcut without release.");
        session.ToggleSkipping(); session.SetSkippingHeld(false, true);
        Check(session.skipping, "Releasing hold input cancelled independently toggled skipping.");
        session.ToggleAutomatic(); session.SetSkippingHeld(true, true); Check(session.skipping && !session.automatic, "Held skip competed with automatic reading.");
        session.SetSkippingHeld(false, true); Check(!session.skipping, "Releasing an owned hold retained skipping.");

        var draft = FirstStoryTemplate.Create(codec); var edits = new StoryEditService(codec);
        var barrier = edits.Add(draft, "opening", StoryNodeIds.system, new SystemNode { operation = SystemOperation.RollbackBarrier, argument = "External action" }, new(400, 250));
        edits.Connect(draft, new(new("hello"), new("next")), barrier); edits.Connect(draft, new(barrier, new("next")), new("sea"));
        var chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        using var bounded = new NovelSession(new() { chapters = [chapter], startChapter = chapter.chapterId }); bounded.StartNew(); bounded.preferences.skipReadOnly = false;
        bounded.SetSkippingHeld(true, true); bounded.Tick(0); bounded.SetSkippingHeld(true, true);
        Check(bounded.story!.nodeId == "sea" && !bounded.skipping && !bounded.story.isTextComplete, "An explicit script barrier failed to retire held skipping.");
        bounded.SetSkippingHeld(false, true); bounded.SetSkippingHeld(true, true); Check(bounded.skipping, "A script barrier permanently disabled future independent hold edges.");
    }

    internal static void ActualUiAndViewportOwnership(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var adapter = new InnoNovelInput(); var viewport = new RenderViewport(0, 0, 1280, 800);
        UiInputSnapshot Map(KeyModifier modifier, bool active = true) => adapter.Map(new(default, true, default, modifier, [], [], [], [], [], active), viewport, 1280, 800);
        Check(Map(KeyModifier.Control).skipHeld, "Formal engine Control modifiers did not reach hold input.");
        Check(!Map(KeyModifier.Control, false).skipHeld && !Map(KeyModifier.Control).skipHeld, "Viewport suspension or return with Control held restarted skipping.");
        Map(KeyModifier.None); Check(Map(KeyModifier.Control).skipHeld, "Viewport release gating never rearmed after physical release.");
        Check(!Map(KeyModifier.Control | KeyModifier.Alt).skipHeld && !Map(KeyModifier.Control).skipHeld, "AltGr or a modifier chord was mistaken for a fresh skip hold.");
        Map(KeyModifier.None); Check(Map(KeyModifier.Control).InCanvas(UiCanvasFit.Create(1920, 1080, new(0, 0, 1280, 800))).skipHeld, "Nested canvas conversion discarded hold input.");

        var game = Game(codec, compiler); using var session = new NovelSession(game); session.StartNew(); session.preferences.skipReadOnly = false;
        using (var view = new NovelGameView(game, session, static (element, text) => (text.Length * element.style.fontSize, element.style.fontSize)))
        {
            view.HandleInput(new() { skipHeld = true, composing = true }); view.HandleInput(new() { skipHeld = true });
            Check(!session.skipping, "Composition or its completion rearmed a held reading shortcut.");
            view.HandleInput(new()); view.HandleInput(new() { skipHeld = true }); Check(session.skipping, "The actual reading view failed to route a permitted new hold.");
            view.HandleInput(new() { skipHeld = true, cancel = true }); Check(session.page == "pause" && !session.skipping, "Menu opening left hold-driven story progression active.");
            view.HandleInput(new() { skipHeld = true, cancel = true }); view.HandleInput(new() { skipHeld = true });
            Check(session.page == "reading" && !session.skipping, "Menu return restarted the same physical hold.");
            view.HandleInput(new());
        }
        var reading = game.screens.Single(static item => item.role == "reading").screen!;
        reading.root.children = [.. reading.root.children, new() { id = "typed", kind = UiElementKind.Input, binding = "variables.player", layout = new() { width = 300, height = 60 } }];
        reading.initialFocus = "typed";
        using (var view = new NovelGameView(game, session, static (element, text) => (text.Length * element.style.fontSize, element.style.fontSize)))
        {
            view.BuildFrame(); view.HandleInput(new() { skipHeld = true }); Check(!session.skipping, "A focused text input did not own Control-based shortcuts."); view.HandleInput(new());
        }
        reading.root.children = reading.root.children.Where(static item => item.id != "typed").ToArray();
        reading.root.children = [.. reading.root.children, new() { id = "selector", kind = UiElementKind.Selector, binding = "game.theme", options = [new() { id = "default", value = NovelValue.String(""), text = "Default" }], layout = new() { width = 300, height = 60 } }];
        reading.initialFocus = "selector";
        using (var view = new NovelGameView(game, session, static (element, text) => (text.Length * element.style.fontSize, element.style.fontSize)))
        {
            view.BuildFrame(); view.HandleInput(new() { confirm = true }); view.HandleInput(new() { skipHeld = true });
            Check(!session.skipping, "A selector popup leaked held input into the narrative.");
            view.HandleInput(new() { skipHeld = true, cancel = true }); view.HandleInput(new() { skipHeld = true });
            Check(session.page == "reading" && !session.skipping, "Dropdown cancellation rearmed an existing hold or escaped into menu navigation.");
        }
    }

    private static NovelProjectAsset Game(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var chapter = FirstStoryTemplate.Create(codec).ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        return new() { chapters = [chapter], startChapter = chapter.chapterId,
            screens = new[] { "title", "reading", "pause", "settings", "history", "credits" }.Select(role => new NovelScreenBinding { role = role, screen = ShellScreenTemplates.Create(role, null!, "Clear") }).ToArray() };
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}

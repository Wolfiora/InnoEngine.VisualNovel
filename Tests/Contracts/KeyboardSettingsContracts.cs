using System;
using System.Linq;
using Inno.Core.Input;
using Inno.Core.Serialization;
using Inno.Rendering;
using Inno.Rendering.Runtime;
using Inno.VisualNovel.Authoring.LocalizationEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Integration.Services;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Player.Binding;
using Inno.VisualNovel.Player.Persistence;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Shell;
using Inno.VisualNovel.Player.Shell.Keyboard;
using Inno.VisualNovel.UI.Input;
using Inno.VisualNovel.UI.Layout;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class KeyboardSettingsContracts
{
    internal static void NativeSettingsAndActualSelectors(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var preferences = new PlayerPreferences(); var exposed = preferences.keyboardShortcuts; exposed[0].key = KeyCode.Q;
        Check(preferences.ResolveKeyboardShortcut([KeyCode.A]) == "automatic", "Preference getters exposed mutable key mappings.");
        preferences.SetKeyboardShortcut("automatic", KeyCode.Q); Reject(() => preferences.SetKeyboardShortcut("skip", KeyCode.Q));
        Reject(() => preferences.SetKeyboardShortcut("skip", KeyCode.Escape)); Reject(() => preferences.keyboardShortcuts = []);
        Check(preferences.ResolveKeyboardShortcut([KeyCode.Q, KeyCode.F9]) == "quickLoad" && preferences.ResolveKeyboardShortcut([KeyCode.S]) == "skip",
            "Rejected mapping changes modified another action or simultaneous key priority depended on collection order.");
        preferences.SetKeyboardShortcut("skip", KeyCode.Unknown); Check(preferences.ResolveKeyboardShortcut([KeyCode.S]) == "", "An explicitly disabled shortcut remained active.");
        var packet = native.Deserialize<PlayerPreferencesState>(native.Serialize(new PlayerPreferencesState { gameId = "keyboard", preferences = preferences }));
        var copied = packet.preferences.Copy(); copied.SetKeyboardShortcut("automatic", KeyCode.T);
        Check(packet.gameId == "keyboard" && packet.preferences.ResolveKeyboardShortcut([KeyCode.Q]) == "automatic" && copied.ResolveKeyboardShortcut([KeyCode.Q]) == "",
            "Native preference persistence or detached copying lost formal engine key values.");
        var source = new KeyboardSettingsDataSource(() => preferences, static (_, text) => text);
        var sourceRow = source.ReadRows("settings.shortcuts").Single(static row => row.key == "automatic");
        preferences = new(); preferences.SetKeyboardShortcut("automatic", KeyCode.T);
        Check(source.ReadRows("settings.shortcuts").Single(static row => row.key == "automatic").values["shortcutKey"].text == "T",
            "A replacement profile left the keyboard data source attached to old settings.");
        Check(!source.TryWrite("item.shortcutKey", NovelValue.String("Q"), sourceRow) && source.ReadRows("settings.shortcutKeys").All(static row => row.values["value"].kind == NovelValueKind.Enum),
            "Shortcut selectors coerced untyped strings or lost exact enum values.");

        var game = Game(codec, compiler); game.localization = StarterLocalizationTemplate.Create(game);
        using var session = new NovelSession(game); session.StartNew(); session.OpenPage("settings");
        using var view = new NovelGameView(game, session, Measure);
        Focus(view, "automatic/keyboard-key"); view.HandleInput(new() { confirm = true });
        Move(view, KeyCode.A, KeyCode.Q); view.HandleInput(new() { confirm = true });
        Check(session.preferences.ResolveKeyboardShortcut([KeyCode.Q]) == "automatic" && session.preferences.ResolveKeyboardShortcut([KeyCode.A]) == "",
            "The actual settings selector did not update the selected stable row.");
        Focus(view, "skip/keyboard-key"); view.HandleInput(new() { confirm = true }); Move(view, KeyCode.S, KeyCode.Q); view.HandleInput(new() { confirm = true });
        Check(session.preferences.ResolveKeyboardShortcut([KeyCode.S]) == "skip" && view.BuildFrame().items.Any(static item => item.id.EndsWith("/binding-error", StringComparison.Ordinal)),
            "A conflicting key silently replaced another binding or closed its error popup.");
        view.HandleInput(new() { cancel = true }); view.HandleInput(new() { confirm = true }); Move(view, KeyCode.S, KeyCode.Unknown); view.HandleInput(new() { confirm = true });
        Check(session.preferences.ResolveKeyboardShortcut([KeyCode.S]) == "", "The actual selector could not disable a reading shortcut.");
        var settings = session.preferences.Copy(); settings.SetKeyboardShortcut("automatic", KeyCode.T); session.ApplyPreferences(settings);
        Check(view.BuildFrame().items.Single(static item => item.id.EndsWith("automatic/keyboard-key", StringComparison.Ordinal)).text.StartsWith("T", StringComparison.Ordinal),
            "The actual settings page retained a discarded profile's key mapping.");
        Focus(view, "keyboard-reset"); view.HandleInput(new() { confirm = true });
        Check(session.preferences.ResolveKeyboardShortcut([KeyCode.A]) == "automatic" && session.preferences.ResolveKeyboardShortcut([KeyCode.S]) == "skip", "The editable reset action did not restore all defaults.");
        session.SetLanguage("en");
        Check(view.BuildFrame().items.Any(static item => item.text == "Toggle automatic reading"), "Dynamic shortcut titles did not use the game language.");
        Check(GlyphCoverageSources.Enumerate(game, "en").Any(static item => item.textId == "keyboard/automatic" && item.text == "Toggle automatic reading"),
            "Known dynamic shortcut labels were absent from source-located glyph coverage.");
        var nestedGame = Game(codec, compiler);
        nestedGame.uiDataSources = [new() { id = "scoped", title = "Scopes", collections = [
            new() { id = "outer", title = "Outer", fields = [new() { schema = new() { id = "outerFlag", defaultValue = NovelValue.Boolean(true) } }] },
            new() { id = "inner", title = "Inner", fields = [new() { schema = new() { id = "shortcutKey", defaultValue = NovelValue.Enum("local"), members = ["local"] }, writable = true }] }] }];
        var nestedPage = new ScreenAsset { root = new() { id = "outer", repeatBinding = "custom.scoped.outer", children = [new() { id = "inner", repeatBinding = "custom.scoped.inner",
            visibleBinding = "item.outerFlag", children = [new() { id = "local-key", kind = UiElementKind.Selector, binding = "item.shortcutKey", options = [new() { id = "local", value = NovelValue.Enum("local") }] }] }] } };
        nestedGame.screens = [new() { role = "reading", screen = nestedPage }];
        UiDataBindingValidator.Validate(nestedGame); UiSelectorValidator.Validate(nestedGame);
        Check(UiDataBindingValidator.RowOwner(nestedPage, "inner", nestedGame.uiDataSources)?.id == "outer"
            && UiDataBindingValidator.RowOwner(nestedPage, "local-key", nestedGame.uiDataSources)?.id == "inner",
            "A repeater's own condition used child rows, or a custom same-name field was interpreted as a built-in shortcut.");
    }

    internal static void FormalMappingAndReadingOwnership(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var game = Game(codec, compiler); using var session = new NovelSession(game); session.StartNew(); session.preferences.SetKeyboardShortcut("automatic", KeyCode.Q);
        var adapter = new InnoNovelInput(); var viewport = new RenderViewport(0, 0, 1280, 800);
        UiInputSnapshot Map(KeyCode key, KeyModifier modifier = KeyModifier.None, bool enabled = true) => adapter.Map(
            new(default, false, default, modifier, [key], [], [], [], [], enabled), viewport, 1920, 1080, session.preferences);
        Check(Map(KeyCode.A).readingShortcut == "" && Map(KeyCode.Q).InCanvas(UiCanvasFit.Create(1920, 1080, new(0, 0, 1280, 800))).readingShortcut == "automatic",
            "Formal viewport mapping ignored active settings or lost the action in nested canvas conversion.");
        var chord = Map(KeyCode.Q, KeyModifier.Control);
        Check(chord.readingShortcut == "" && !chord.skipHeld && Map(KeyCode.Q, enabled: false).readingShortcut == "", "Modifier chords or suspended outputs dispatched reading actions.");
        var selection = Map(KeyCode.A, KeyModifier.Control).InCanvas(UiCanvasFit.Create(1920, 1080, new(0, 0, 1280, 800)));
        Check(selection.selectAll && !Map(KeyCode.A, KeyModifier.Control | KeyModifier.Alt).selectAll
            && Map(KeyCode.LeftArrow, KeyModifier.Control | KeyModifier.Shift) is { textLeft: true, decrement: false, wordNavigation: true, selecting: true }
            && Map(KeyCode.Tab, KeyModifier.Shift) is { previous: true, next: false } && Map(KeyCode.Tab) is { next: true, previous: false }
            && Map(KeyCode.Delete).delete && Map(KeyCode.Home).home && Map(KeyCode.End).end,
            "Formal text/navigation mapping lost modifier ownership, AltGr safety, canvas propagation or backward Tab navigation.");
        using (var view = new NovelGameView(game, session, Measure))
        {
            view.HandleInput(Map(KeyCode.Q)); Check(session.automatic && !session.story!.isTextComplete, "A mapped reading shortcut also advanced the dialogue.");
            view.HandleInput(new() { readingShortcut = "automatic", composing = true }); Check(session.automatic, "Composition triggered a mapped action.");
            Focus(view, "pause");
            string focused = view.BuildFrame().items.FirstOrDefault(static item => item.focused)?.id ?? "none";
            view.HandleInput(new() { readingShortcut = "automatic", confirm = true });
            Check(session.page == "pause" && session.automatic, "One input both activated the focused menu control and toggled a reading action. Focus=" + focused + ", page=" + session.page + ", automatic=" + session.automatic);
            view.HandleInput(new() { readingShortcut = "automatic" }); Check(session.automatic, "A modal page leaked a reading shortcut.");
            view.HandleInput(new() { cancel = true });
            view.HandleInput(new() { readingShortcut = "quickLoad" }); view.HandleInput(new() { readingShortcut = "quickSave" });
            Check(session.page == "reading" && session.automatic, "Unavailable native persistence was exposed by shortcut dispatch.");
            view.HandleInput(new() { readingShortcut = "history" }); Check(session.page == "history", "The history shortcut bypassed or failed ordinary screen routing.");
            view.HandleInput(new() { cancel = true });
            view.HandleInput(new() { readingShortcut = "automatic" }); Check(!session.automatic, "The remapped toggle failed after modal focus retirement.");
        }
        var reading = game.screens.Single(static item => item.role == "reading").screen!;
        reading.root.children = [.. reading.root.children, new() { id = "text-owner", kind = UiElementKind.Input, binding = "variables.player", layout = new() { width = 300, height = 70 } }];
        reading.initialFocus = "text-owner";
        using (var view = new NovelGameView(game, session, Measure))
        {
            view.HandleInput(new() { readingShortcut = "automatic", text = ["q"] });
            Check(!session.automatic && session.story!.GetVariable("player").text.EndsWith("q", StringComparison.Ordinal), "A typed letter triggered reading instead of remaining with its input control.");
        }
    }

    private static void Focus(
        NovelGameView view,
        string suffix
    ) {
        for (int count = 0; count < 100; count++)
        {
            if (view.BuildFrame().items.Any(item => item.focused && item.id.EndsWith(suffix, StringComparison.Ordinal))) return;
            view.HandleInput(new() { next = true });
        }
        throw new InvalidOperationException("Keyboard navigation could not reveal settings control: " + suffix);
    }
    private static void Move(
        NovelGameView view,
        KeyCode from,
        KeyCode to
    ) {
        var keys = NovelKeyboardShortcuts.keys.ToList(); int delta = keys.IndexOf(to) - keys.IndexOf(from);
        for (int count = 0; count < Math.Abs(delta); count++) view.HandleInput(new() { next = delta > 0, previous = delta < 0 });
    }
    internal static NovelProjectAsset Game(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var chapter = FirstStoryTemplate.Create(codec).ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        return new() { chapters = [chapter], startChapter = chapter.chapterId, screens = new[] { "title", "reading", "pause", "settings", "history", "credits" }
            .Select(role => new NovelScreenBinding { role = role, screen = ShellScreenTemplates.Create(role, null!, "Clear") }).ToArray() };
    }
    private static (float, float) Measure(
        UiElementDefinition element,
        string text
    ) => (text.Length * element.style.fontSize, element.style.fontSize);
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
    private static void Reject(Action operation) { try { operation(); } catch (ArgumentException) { return; } throw new InvalidOperationException("An invalid keyboard mapping was accepted."); }
}

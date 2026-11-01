using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.Text;
using Inno.VisualNovel.Authoring.Diagnostics;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Player.Binding;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Shell;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class ThemeSessionContracts
{
    internal static void SwitchingSaveRollback(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var chapter = FirstStoryTemplate.Create(codec).ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var game = new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId,
            screens = new[] { "title", "reading", "pause", "settings", "history", "credits" }.Select(role => new NovelScreenBinding
            { role = role, screen = ShellScreenTemplates.Create(role, new FontAsset(), "Clear") }).ToArray() };
        var reading = game.screens.Single(static binding => binding.role == "reading").screen!;
        reading.root.children.Single(static element => element.id == "dialogue").styleClass = "panel";
        game.themes = [Theme("day", 0xEEE5D5FF), Theme("night", 0x121822FF)]; game.defaultTheme = "day";
        var settings = game.screens.Single(static binding => binding.role == "settings").screen!;
        settings.initialFocus = "theme-button";
        settings.root.children = [new() { id = "theme-button", kind = UiElementKind.Button, text = "切换", action = "theme", argument = "night", layout = new() { width = 300, height = 90 } },
            new() { id = "back", kind = UiElementKind.Button, text = "返回", action = "back", layout = new() { x = 400, width = 200, height = 90 } }];
        using var session = new NovelSession(game);
        using var view = new NovelGameView(game, session, static (_, _) => (120, 40));
        game.themes[0].styles[0].values.background = 0; reading.root.children = [];
        session.StartNew();
        Check(view.BuildFrame().items.Single(static item => item.id == "dialogue").background == 0xEEE5D5FF, "Session or view retained mutable source theme/page values.");
        var data = new NovelSessionDataSource(session);
        Check(data.ReadRows("themes").Count == 2 && data.TryRead("game.theme", out var selected) && selected.text == "day", "Theme selectors lack typed choices or their current value.");
        view.HandleInput(new() { pointerInside = true, x = 120, y = 1020, pressed = true });
        view.HandleInput(new() { pointerInside = true, x = 120, y = 1020, released = true });
        Check(session.page == "pause", "The normal modal path did not open.");
        session.OpenPage("settings"); view.BuildFrame(); view.HandleInput(new() { confirm = true });
        Check(session.themeId == "night" && session.page == "settings" && session.story!.nodeId == "hello", "Theme action restarted narrative or destroyed the active modal page.");
        view.BuildFrame(); view.HandleInput(new() { next = true }); view.HandleInput(new() { confirm = true });
        Check(session.page == "reading", "The existing modal return path was lost when rebuilding themed screens.");
        var saved = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture()));
        Check(saved.themeId == "night", "Native save omitted the selected theme.");
        session.SetTheme("day"); session.Restore(saved);
        Check(session.themeId == "night" && view.BuildFrame().items.Single(static item => item.id == "dialogue").background == 0x121822FF, "Candidate loading did not restore the exact theme.");
        saved.themeId = "removed"; Reject(() => session.Restore(saved));
        Check(session.themeId == "night" && session.story!.nodeId == "hello", "A missing-theme save damaged the active session.");
        session.StartNew(); session.SetTheme("day"); session.Advance(); session.Advance(); session.SetTheme("night"); session.Choose("sea");
        Check(session.Rollback() && session.themeId == "day", "Rollback restored story without its boundary's theme.");
        Reject(() => data.TryWrite("game.theme", NovelValue.String("absent")));
        Check(session.themeId == "day", "Rejected selector value changed the active theme.");
    }

    internal static void ValidateEveryVariant(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var chapter = FirstStoryTemplate.Create(codec).ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var game = new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId,
            screens = new[] { "title", "reading", "pause", "settings", "history", "credits" }.Select(role => new NovelScreenBinding
            { role = role, screen = ShellScreenTemplates.Create(role, new FontAsset(), "Clear") }).ToArray(), themes = [Theme("day", 0xEEE5D5FF)], defaultTheme = "day" };
        ThemeProjectValidator.Validate(game);
        var selector = new UiElementDefinition { id = "theme", kind = UiElementKind.Selector, binding = "game.theme", optionsBinding = "themes" };
        var settings = game.screens.Single(static page => page.role == "settings").screen!; settings.root.children = [selector]; settings.initialFocus = "theme"; UiSelectorValidator.Validate(game);
        selector.optionsBinding = "languages"; Reject(() => UiSelectorValidator.Validate(game)); selector.optionsBinding = "themes";
        game.themes[0].screens = [new() { role = "reading", screen = new() { root = new() { id = "root" } } }];
        Reject(() => ThemeProjectValidator.Validate(game));
        game.themes[0].screens = [new() { role = "undeclared", screen = new() }]; Reject(() => new NovelSession(game));
        game.themes[0].screens = []; game.defaultTheme = "missing"; Reject(() => new NovelSession(game));
    }

    private static ThemeAsset Theme(
        string id,
        uint color
    ) => new() { resourceId = id, title = id, styles = [new() { key = "panel", fields = UiStyleProperty.Background, values = new() { background = color } }] };
    private static void Reject(Action action) { try { action(); } catch (InvalidOperationException) { return; } throw new InvalidOperationException("Invalid theme candidate was accepted."); }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

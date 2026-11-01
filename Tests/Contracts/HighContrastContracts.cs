using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.Text;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Text;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Player.Persistence;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Shell;
using Inno.VisualNovel.UI.Binding;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class HighContrastContracts
{
    internal static void PaintGeometryRichTextAndImages()
    {
        var screen = new ScreenAsset { width = 640, height = 360, root = new() { id = "root", style = new() { background = 0 }, children = [
            new() { id = "line", kind = UiElementKind.Text, text = "[color=#222222][b]中[/b][/color][ruby=かん]漢[/ruby][pause=0.2]Á",
                richText = true, layout = new() { width = 500, height = 90 }, style = new() { foreground = 0x111111FF } },
            new() { id = "advance", kind = UiElementKind.Button, action = "advance", layout = new() { y = 120, width = 300, height = 100 }, style = new() { background = 0 } },
            new() { id = "art", kind = UiElementKind.Image, layout = new() { x = 510, width = 80, height = 80 }, style = new() { imageTint = 0xAABBCCFF, background = 0 } } ] } };
        var document = new UiDocumentInstance(screen, Measure); var data = new Empty(); var normal = document.BuildFrame(data);
        document.SetHighContrast(true); var readable = document.BuildFrame(data);
        var text = readable.items.Single(static item => item.id == "line"); var original = normal.items.Single(static item => item.id == "line");
        Check(text.bounds == original.bounds && text.clip == original.clip && text.foreground == 0xFFFFFFFF && text.background == 0x000000FF
            && text.richText!.document.runs.All(static run => run.style.color == 0xFFFFFFFF) && text.richText.document.runs.Any(static run => run.style.bold)
            && text.richText.document.runs.Any(static run => run.ruby == "かん") && text.richText.document.beats.Count == original.richText!.document.beats.Count,
            "High contrast lost layout, emphasis, ruby, pacing or allowed a dark rich-text override.");
        Check(readable.items.Single(static item => item.id == "advance").background == 0 && readable.items.Single(static item => item.id == "art").imageTint == 0xAABBCCFF,
            "High contrast obscured the stage through an invisible advance surface or recolored artwork.");
        var result = document.HandleInput(new() { x = 30, y = 150, pointerInside = true, pressed = true }, data);
        result = document.HandleInput(new() { x = 30, y = 150, pointerInside = true, released = true }, data);
        Check(result.actions.Single().action == "advance", "The accessible paint variant changed input routing.");
        document.SetHighContrast(false); var reverted = document.BuildFrame(data).items.Single(static item => item.id == "line");
        Check(reverted.foreground == original.foreground && reverted.richText!.document.runs[0].style.color == original.richText!.document.runs[0].style.color
            && screen.root.children[0].style.foreground == 0x111111FF, "Turning accessibility off could not recover the untouched author design.");
    }
    internal static void ActualSettingsThemesAndNativePreferences(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var story = FirstStoryTemplate.Create(codec).ToAsset(); story.program = compiler.Compile(story).program!;
        foreach (string name in ShellThemeTemplates.names)
        {
            var theme = ShellThemeTemplates.Create(name, new FontAsset());
            var game = new NovelProjectAsset { chapters = [story], startChapter = story.chapterId, screens = theme.screens, themes = [theme], defaultTheme = theme.resourceId };
            using var session = new NovelSession(game); session.StartNew(); var snapshot = session.Capture(); session.OpenPage("settings");
            using var view = new NovelGameView(game, session, Measure);
            for (int index = 0; index < 100 && !view.BuildFrame().items.Any(static item => item.id == "high-contrast-control" && item.focused); index++) view.HandleInput(new() { next = true });
            Check(view.BuildFrame().items.Any(static item => item.id == "high-contrast-control" && item.focused), "A complete theme lacked a keyboard-accessible high-contrast setting: " + name);
            view.HandleInput(new() { confirm = true }); var frame = view.BuildFrame();
            Check(session.preferences.highContrast && session.themeId == theme.resourceId && frame.items.Where(static item => item.text.Length > 0).All(static item => item.foreground == 0xFFFFFFFF),
                "The actual settings toggle did not update the player-owned palette or replaced the author's theme: " + name);
            var copied = session.preferences.Copy(); session.Restore(snapshot);
            Check(session.preferences.highContrast && copied.highContrast, "Narrative restoration reset the player's accessibility preference.");
            var packet = native.Deserialize<PlayerPreferencesState>(native.Serialize(new PlayerPreferencesState { gameId = session.gameId, preferences = copied }));
            Check(packet.preferences.highContrast && BuiltInUiChoices.WritableBooleans([]).Any(static option => option.id == "settings.highContrast"),
                "The preference was absent from native persistence or no-code binding choices.");
        }
    }
    private sealed class Empty : UiDataSource
    {
        public override bool TryRead(string path, out NovelValue value) { value = NovelValue.Boolean(false); return false; }
        public override bool TryWrite(string path, NovelValue value) => false;
    }
    private static (float width, float height) Measure(
        UiElementDefinition element,
        string text
    ) => (text.Length * element.style.fontSize / 2, element.style.fontSize * 1.5f);
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}

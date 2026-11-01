using System;
using System.Linq;
using InnoEngine.Text;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.UI.Theming;

namespace Inno.VisualNovel.Authoring.Templates;

internal static class ThemeScreenComposition
{
    internal static ScreenAsset Create(
        string role,
        FontAsset font,
        string name,
        ThemeAsset theme
    ) {
        ScreenAsset screen = ShellScreenTemplates.Create(role, font, name); screen.title = name + " · " + role;
        foreach (var row in ScreenEditService.Flatten(screen))
        {
            var element = row.element; float size = element.style.fontSize;
            element.styleClass = element.id == "background" ? "screen" : element.id == "accent-line" ? "accent.fill"
                : element.kind is UiElementKind.Button or UiElementKind.Toggle or UiElementKind.Selector or UiElementKind.Slider or UiElementKind.Input ? element.id == "advance-surface" ? "" : "button"
                : element.kind == UiElementKind.Panel && (element.style.background & 255) > 0 && element.id != "player-input-overlay" ? "panel"
                : element.kind == UiElementKind.Text ? element.binding is "dialogue.text" or "dialogue.pageText" ? "reading"
                    : element.binding == "dialogue.speaker" || element.id == "eyebrow" ? "accent.text" : size >= 70 ? "title" : size >= 46 ? "heading" : size < 28 ? "caption" : "body" : "";
            element.styleOverrides = UiStyleProperty.None;
            if (element.kind is UiElementKind.Horizontal or UiElementKind.Vertical)
            { element.layout.heightMode = UiSizeMode.Content; element.layout.minHeight = Math.Min(element.layout.height, 80); }
            if (element.styleClass == "") continue;
            if (element.kind is UiElementKind.Button or UiElementKind.Toggle or UiElementKind.Selector or UiElementKind.Input)
            { element.layout.minHeight = Math.Max(68, element.layout.minHeight); element.layout.heightMode = UiSizeMode.Content; }
            if (row.parent?.kind is UiElementKind.Horizontal or UiElementKind.Vertical && element.kind == UiElementKind.Text) element.layout.heightMode = UiSizeMode.Content;
        }
        if (role == "title") Title(screen, name);
        if (role == "reading") Reading(screen, name);
        if (role == "settings")
        {
            var settings = ScreenEditService.Find(screen, "settings-list"); settings.children = [new()
            {
                id = "theme-row", kind = UiElementKind.Horizontal, blocksPointer = false,
                layout = new() { widthMode = UiSizeMode.Fill, heightMode = UiSizeMode.Content, minHeight = 90, gap = 16 }, children = [
                    new() { id = "theme-label", kind = UiElementKind.Text, text = "界面主题", textId = StoryTextKeys.Ui(role, "theme-label"), styleClass = "body", blocksPointer = false,
                        layout = new() { width = 600, heightMode = UiSizeMode.Content } },
                    new() { id = "theme-selector", kind = UiElementKind.Selector, binding = "game.theme", optionsBinding = "themes", styleClass = "button",
                        layout = new() { width = 800, minHeight = 76, heightMode = UiSizeMode.Content } }] }, .. settings.children];
            foreach (var row in settings.children) { row.layout.heightMode = UiSizeMode.Content; row.layout.minHeight = 90; row.layout.widthMode = UiSizeMode.Fill; }
        }
        if (role == "credits")
        {
            var credits = ScreenEditService.Find(screen, "credits"); credits.layout.x = credits.layout.y = 0; credits.layout.widthMode = UiSizeMode.Fill; credits.layout.heightMode = UiSizeMode.Content;
            screen.root.children = [.. screen.root.children.Where(element => element.id != "credits"), new() { id = "credits-scroll", kind = UiElementKind.Scroll, blocksPointer = true,
                layout = new() { x = 112, y = 360, width = 1696, height = 540, padding = 18, clip = true }, children = [credits] }];
        }
        if (role == "pause")
        {
            var menu = ScreenEditService.Find(screen, "pause-menu");
            menu.kind = UiElementKind.Scroll; menu.layout.heightMode = UiSizeMode.Fixed;
            menu.layout.height = 580; menu.layout.padding = 8; menu.layout.clip = true;
            foreach (var button in menu.children) button.layout.widthMode = UiSizeMode.Fill;
        }
        if (role == "confirm") screen.initialFocus = "confirm-cancel";
        if (role == "pause") screen.initialFocus = "resume";
        if (role is "extras" or "gallery" or "music" or "replays" or "endings" or "glossary" or "characters" or "collectionDetail") screen.initialFocus = "back";
        var resolver = new UiThemeResolver(WithoutScreens(ThemeAssetCopy.Copy(theme)));
        var resolved = resolver.Apply(screen);
        CopyFallback(screen.root, resolved.root);
        return screen;
    }
    private static ThemeAsset WithoutScreens(ThemeAsset theme) { theme.screens = []; return theme; }
    private static void CopyFallback(
        UiElementDefinition target,
        UiElementDefinition resolved
    ) {
        target.style = resolved.style.Copy();
        for (int index = 0; index < target.children.Length; index++) CopyFallback(target.children[index], resolved.children[index]);
    }
    private static void Title(
        ScreenAsset screen,
        string name
    ) {
        var title = ScreenEditService.Find(screen, "title"); var subtitle = ScreenEditService.Find(screen, "subtitle"); var eyebrow = ScreenEditService.Find(screen, "eyebrow"); var menu = ScreenEditService.Find(screen, "menu");
        foreach (var element in new[] { title, subtitle, eyebrow, menu }) { element.layout.x = element.layout.y = 0; element.layout.widthMode = UiSizeMode.Fill; element.layout.heightMode = UiSizeMode.Content; }
        title.layout.minHeight = 120; menu.layout.gap = 18;
        foreach (var button in menu.children) { button.layout.widthMode = UiSizeMode.Fill; button.layout.heightMode = UiSizeMode.Content; button.layout.minHeight = 80; }
        if (name == "Cinema") menu.kind = UiElementKind.Horizontal;
        var column = new UiElementDefinition { id = "title-composition", kind = UiElementKind.Vertical, blocksPointer = false,
            layout = new() { x = name == "Paper" ? 420 : name == "Cinema" ? 180 : 112, y = name == "Cinema" ? 260 : 150,
                width = name == "Paper" ? 1080 : name == "Cinema" ? 1560 : 1000, heightMode = UiSizeMode.Content, gap = name == "Paper" ? 24 : 32 }, children = [eyebrow, title, subtitle, menu, ScreenEditService.Find(screen, "title-save-message")] };
        var saveMessage = column.children[^1]; saveMessage.layout.x = saveMessage.layout.y = 0; saveMessage.layout.widthMode = UiSizeMode.Fill; saveMessage.layout.heightMode = UiSizeMode.Content;
        if (name != "Cinema") menu.layout.widthMode = UiSizeMode.Fixed;
        menu.layout.width = name == "Paper" ? 1080 : 640;
        var background = ScreenEditService.Find(screen, "background"); var accent = ScreenEditService.Find(screen, "accent-line");
        accent.layout.x = name == "Paper" ? 340 : 1740; accent.layout.y = 120; accent.layout.width = name == "Cinema" ? 2 : 5; accent.layout.height = 800;
        screen.root.children = name == "Paper" ? [background, new() { id = "paper-sheet", kind = UiElementKind.Panel, styleClass = "panel", blocksPointer = false,
            layout = new() { x = 340, y = 90, width = 1240, height = 900 } }, accent, column] : [background, accent, column];
        screen.root.children = [.. screen.root.children.Where(element => element.id != "title-composition"), new() { id = "title-scroll", kind = UiElementKind.Scroll,
            blocksPointer = false, layout = new() { x = column.layout.x, y = 100, width = column.layout.width, height = 900, clip = true, padding = 8 }, children = [column] }];
        column.layout.x = 0; column.layout.y = 0; column.layout.widthMode = UiSizeMode.Fill;
    }
    private static void Reading(
        ScreenAsset screen,
        string name
    ) {
        var dialogue = ScreenEditService.Find(screen, "dialogue"); var speaker = ScreenEditService.Find(screen, "speaker"); var scroll = ScreenEditService.Find(screen, "dialogue-scroll");
        dialogue.layout.x = name == "Paper" ? 180 : name == "Cinema" ? 220 : 96;
        dialogue.layout.y = 630; dialogue.layout.width = name == "Paper" ? 1560 : name == "Cinema" ? 1480 : 1728; dialogue.layout.height = 280;
        ReadingPresentationTemplates.AddPortrait(screen);
        var toolbar = ScreenEditService.Find(screen, "toolbar"); toolbar.layout.y = -20; toolbar.layout.anchorY = toolbar.layout.pivotY = 1; toolbar.layout.heightMode = UiSizeMode.Content;
        foreach (var button in toolbar.children) { button.layout.widthMode = UiSizeMode.Fill; button.layout.heightMode = UiSizeMode.Content; button.layout.minHeight = 76; }
        var option = ScreenEditService.Find(screen, "option"); option.text = option.binding = string.Empty; option.layout.heightMode = UiSizeMode.Content; option.layout.padding = 16; option.layout.minHeight = 90;
        var mark = option.children.Single(); mark.layout.x = mark.layout.y = 0; mark.layout.heightMode = UiSizeMode.Content; mark.layout.width = 40;
        option.children = [new() { id = "option-content", kind = UiElementKind.Horizontal, blocksPointer = false,
            layout = new() { widthMode = UiSizeMode.Fill, heightMode = UiSizeMode.Content, gap = 12 }, children = [mark, new() { id = "option-body", kind = UiElementKind.Vertical, blocksPointer = false,
                layout = new() { widthMode = UiSizeMode.Fill, heightMode = UiSizeMode.Content, gap = 8 }, children = [
                    new() { id = "option-label", kind = UiElementKind.Text, binding = "item.text", enabledBinding = "item.enabled", styleClass = "option.text", blocksPointer = false,
                        layout = new() { widthMode = UiSizeMode.Fill, heightMode = UiSizeMode.Content } },
                    new() { id = "option-reason", kind = UiElementKind.Text, binding = "item.reason", visibleBinding = "item.hasReason", styleClass = "caption", blocksPointer = false,
                        layout = new() { widthMode = UiSizeMode.Fill, heightMode = UiSizeMode.Content } }] }] }];
        foreach (string id in new[] { "nvl-page", "letter-page" })
        { var page = ScreenEditService.Find(screen, id); page.layout.y = 80; page.layout.height = 800; }
    }
}

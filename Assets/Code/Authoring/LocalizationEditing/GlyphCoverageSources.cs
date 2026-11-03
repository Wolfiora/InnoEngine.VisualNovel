using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Assets;
using InnoEngine.Input;
using InnoEngine.Text;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Programs;
using Inno.VisualNovel.Model.Collections;
using Inno.VisualNovel.Model.Text;
using Inno.VisualNovel.Player.Localization;
using Inno.VisualNovel.UI.Documents;
using Inno.VisualNovel.UI.Text;
using Inno.VisualNovel.UI.Theming;
using Inno.VisualNovel.Authoring.Diagnostics;

namespace Inno.VisualNovel.Authoring.LocalizationEditing;

/// <summary>Enumerates static saved content in the same page/theme font contexts used by the player.</summary>
public static class GlyphCoverageSources
{
    private sealed record Page(string role, ScreenAsset screen, AssetPath path, UiElementDefinition[] elements, UiRichTextResources resources, string theme);
    private sealed record Chapter(string chapterId, AssetPath assetPath, StoryProgram program);
    /// <summary>Visits every fallback/theme presentation while resolving the selected game's language policy.</summary>
    /// <param name="project">Saved compiled game roots; the caller may supply a detached localization draft.</param>
    /// <param name="language">The explicit target language.</param>
    /// <returns>Owner-thread source contexts; dynamic variable values remain outside static coverage.</returns>
    public static IEnumerable<GlyphCoverageDocument> Enumerate(
        NovelProjectAsset project,
        string language
    ) {
        var texts = new NovelLocalizationService(project.localization, project.defaultLanguage).ForLanguage(language);
        var chapters = project.chapters.Select(static chapter => new Chapter(chapter.chapterId, chapter.assetPath,
            StoryProgramCopy.Copy(chapter.program ?? throw new AuthoringValidationException($"Save and compile the chapters before checking glyph coverage.")))).ToArray();
        var characters = project.characters.Select(static character => (character.resourceId, character.displayName)).ToArray();
        var entries = (project.collections?.entries ?? []).Select(CollectionEntryCopy.Copy).ToArray();
        string title = project.title;
        var contexts = new ThemeAsset?[] { null }.Concat(project.themes).Select(theme =>
        {
            return NovelThemeScreens.Resolve(project.screens, theme).Where(static binding => binding.screen is not null).Select(binding =>
            {
                var screen = theme is null ? UiDefinitionCopy.Copy(binding.screen!) : new UiThemeResolver(theme).Apply(binding.screen!);
                screen.font ??= theme?.font ?? project.font;
                return new Page(binding.role, screen, binding.screen!.assetPath, ScreenEditService.Flatten(screen).Select(static row => row.element).ToArray(),
                    texts.resources.Text(new(screen.textFonts, screen.textIcons)), theme?.resourceId ?? "Page defaults");
            }).ToArray();
        }).ToArray();
        return EnumerateFrozen(contexts, chapters, characters, entries, title, texts);
    }

    private static IEnumerable<GlyphCoverageDocument> EnumerateFrozen(
        Page[][] contexts,
        Chapter[] chapters,
        (string resourceId, string displayName)[] characters,
        CollectionEntry[] entries,
        string title,
        NovelLocalizationService texts
    ) {
        foreach (var pages in contexts)
        {
            foreach (var shortcut in Player.Shell.Keyboard.NovelKeyboardShortcuts.entries)
                foreach (var item in Bound("settings", ["item.name"], "keyboard/" + shortcut.action, texts.Resolve("keyboard/" + shortcut.action, shortcut.title), false, false)) yield return item;
            foreach (var shortcut in Player.Shell.Keyboard.NovelNavigationShortcuts.entries)
                foreach (var item in Bound("settings", ["item.name"], "keyboard/navigation/" + shortcut.action, texts.Resolve("keyboard/navigation/" + shortcut.action, shortcut.title), false, false)) yield return item;
            foreach (var key in Player.Shell.Keyboard.NovelKeyboardShortcuts.choices.Concat(Player.Shell.Keyboard.NovelNavigationShortcuts.choices).DistinctBy(static key => key.id))
                foreach (var item in Bound("settings", ["item.shortcutKey"], "keyboard/key/" + key.id,
                    key.key == KeyCode.Unknown ? texts.Resolve("keyboard/unassigned", "未设置") : key.title, false, false)) yield return item;
            foreach (var page in pages)
                foreach (var element in page.elements)
                {
                    if (element.kind == UiElementKind.Selector)
                        foreach (var message in new[] { ("search", "输入以筛选"), ("no-results", "没有匹配项"), ("rejected", "绑定拒绝此值") })
                            yield return Make(page, element, "ui/selector/" + message.Item1, texts.Resolve("ui/selector/" + message.Item1, message.Item2), false, false);
                    if (element.text.Length > 0) yield return Make(page, element, element.textId.Length == 0 ? StoryTextKeys.Ui(page.role, element.id) : element.textId,
                        texts.Resolve(element.textId, element.text), element.richText, false);
                    if (element.tooltip.Length > 0) yield return Make(page, element, element.tooltipTextId.Length == 0 ? StoryTextKeys.Ui(page.role, element.id + "/tooltip") : element.tooltipTextId,
                        texts.Resolve(element.tooltipTextId, element.tooltip), false, false);
                    foreach (var option in element.options)
                        yield return Make(page, element, option.textId.Length == 0 ? StoryTextKeys.Ui(page.role, element.id + "/" + option.id) : option.textId, texts.Resolve(option.textId, option.text), false, false);
                }
            foreach (var chapter in chapters)
                foreach (var instruction in chapter.program.instructions)
                {
                    string node = instruction.sourceNodeId.Length == 0 ? instruction.nodeId : instruction.sourceNodeId;
                    if (instruction.dialogue is { } line)
                    {
                        string id = StoryTextKeys.Dialogue(line.textId), body = texts.Resolve(id, line.text);
                        foreach (var item in Bound("reading", ["dialogue.text", "dialogue.pageText"], id, body, true, true, chapter, node)) yield return item;
                        foreach (var item in Bound("history", ["item.text"], id, NovelTextMarkup.Parse(body).text, false, true, chapter, node)) yield return item;
                        foreach (var item in Bound("reading", ["dialogue.speaker"], StoryTextKeys.Speaker(line.textId), texts.Resolve(StoryTextKeys.Speaker(line.textId), line.speaker), false, true, chapter, node)) yield return item;
                    }
                    string Key(string field) => StoryTextKeys.Node(chapter.chapterId, node, field);
                    if (instruction.choice is { } choice)
                    {
                        foreach (var item in Bound("reading", ["choice.prompt"], Key("choice.prompt"), texts.Resolve(Key("choice.prompt"), choice.prompt), false, true, chapter, node)) yield return item;
                        foreach (var option in choice.options)
                        {
                            foreach (var item in Bound("reading", ["item.text"], Key("option/" + option.id), texts.Resolve(Key("option/" + option.id), option.text), false, true, chapter, node)) yield return item;
                            foreach (var item in Bound("reading", ["item.reason"], Key("reason/" + option.id), texts.Resolve(Key("reason/" + option.id), option.disabledReason), false, true, chapter, node)) yield return item;
                        }
                    }
                    if (instruction.input is { } input)
                        foreach (var item in Bound("reading", ["input.prompt"], Key("input.prompt"), texts.Resolve(Key("input.prompt"), input.prompt), false, true, chapter, node)) yield return item;
                }
            foreach (var character in characters)
                foreach (var item in Bound(null, ["dialogue.speaker", "item.speaker", "item.name"], StoryTextKeys.CharacterName(character.resourceId), texts.Resolve(StoryTextKeys.CharacterName(character.resourceId), character.displayName), false, false)) yield return item;
            foreach (var item in Bound(null, ["game.title"], "game/title", texts.Resolve("game/title", title), false, false)) yield return item;
            foreach (var entry in entries)
            {
                foreach (var item in Bound(null, ["collection.title", "item.title", "item.name"], StoryTextKeys.Collection(entry.id, "title"), texts.Resolve(StoryTextKeys.Collection(entry.id, "title"), entry.title), false, false)) yield return item;
                foreach (var item in Bound(null, ["collection.description", "item.description"], StoryTextKeys.Collection(entry.id, "description"), texts.Resolve(StoryTextKeys.Collection(entry.id, "description"), entry.description), false, false)) yield return item;
                foreach (var variant in entry.variants)
                    foreach (var item in Bound("collectionDetail", ["collection.variantName"], StoryTextKeys.Collection(entry.id, "variant/" + variant.id + "/title"), texts.Resolve(StoryTextKeys.Collection(entry.id, "variant/" + variant.id + "/title"), variant.title), false, false)) yield return item;
            }

            GlyphCoverageDocument Make(
                Page page,
                UiElementDefinition element,
                string id,
                string text,
                bool rich,
                bool interpolated,
                Chapter? chapter = null,
                string node = ""
            ) => new(id, page.theme + "/" + page.role + "/" + element.id + (chapter is null ? "" : " ← " + chapter.chapterId + "/" + node), text, rich, interpolated,
                texts.resources.primaryFont ?? element.font ?? page.screen.font, element.style.fontSize, page.resources, chapter?.assetPath ?? page.path, node, chapter is null ? element.id : "");
            IEnumerable<GlyphCoverageDocument> Bound(
                string? role,
                string[] bindings,
                string id,
                string text,
                bool rich,
                bool interpolated,
                Chapter? chapter = null,
                string node = ""
            ) {
                if (text.Length == 0) yield break;
                foreach (var page in pages.Where(page => role is null || page.role == role))
                    foreach (var element in page.elements.Where(element => bindings.Contains(element.binding, StringComparer.Ordinal) || bindings.Contains(element.tooltipBinding, StringComparer.Ordinal)))
                        yield return Make(page, element, id, text, bindings.Contains(element.binding, StringComparer.Ordinal) && element.binding != "dialogue.speaker" && (rich || element.richText), interpolated, chapter, node);
            }
        }
    }
}

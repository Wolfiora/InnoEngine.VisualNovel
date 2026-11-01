using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Input;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Model.Project;

namespace Inno.VisualNovel.Authoring.LocalizationEditing;

/// <summary>Extracts stable text identities from imported content without maintaining another story representation.</summary>
public static class LocalizationExtractionService
{
    /// <summary>Collects authored text from compiled source mappings and editable screen definitions.</summary>
    /// <param name="project">The explicit content roots after saving authoring drafts.</param>
    /// <returns>Unique source rows in identity order; conflicting reuse throws.</returns>
    public static IReadOnlyList<LocalizedText> Extract(NovelProjectAsset project)
    {
        var rows = new Dictionary<string, string>(StringComparer.Ordinal);
        Add("game/title", project.title);
        Add("collection/locked", "未解锁");
        Add("keyboard/unassigned", "未设置");
        Add("keyboard/conflict", "快捷键组合已被占用或不能用于此操作。请先更换另一项，或选择未设置。");
        Add("ui/selector/search", "输入以筛选");
        Add("ui/selector/no-results", "没有匹配项");
        Add("ui/selector/rejected", "绑定拒绝此值");
        foreach (var shortcut in Player.Shell.Keyboard.NovelKeyboardShortcuts.entries) Add("keyboard/" + shortcut.action, shortcut.title);
        foreach (var shortcut in Player.Shell.Keyboard.NovelNavigationShortcuts.entries) Add("keyboard/navigation/" + shortcut.action, shortcut.title);
        foreach (var character in project.characters) Add(StoryTextKeys.CharacterName(character.resourceId), character.displayName);
        foreach (var entry in project.collections?.entries ?? [])
        {
            Add(StoryTextKeys.Collection(entry.id, "title"), entry.title);
            Add(StoryTextKeys.Collection(entry.id, "description"), entry.description);
            foreach (var variant in entry.variants) Add(StoryTextKeys.Collection(entry.id, "variant/" + variant.id + "/title"), variant.title);
        }
        foreach (var chapter in project.chapters)
            foreach (var instruction in chapter.program?.instructions ?? throw new InvalidOperationException("Save and compile each chapter before extracting translations."))
            {
                if (instruction.dialogue is { } line)
                { Add(StoryTextKeys.Dialogue(line.textId), line.text); if (line.speaker.Length > 0 || line.characterId.Length == 0) Add(StoryTextKeys.Speaker(line.textId), line.speaker); }
                string Node(string field) => StoryTextKeys.Node(chapter.chapterId, instruction.sourceNodeId.Length > 0 ? instruction.sourceNodeId : instruction.nodeId, field);
                if (instruction.choice is { } choice)
                {
                    Add(Node("choice.prompt"), choice.prompt);
                    foreach (var option in choice.options) { Add(Node("option/" + option.id), option.text); Add(Node("reason/" + option.id), option.disabledReason); }
                }
                if (instruction.input is { } input) Add(Node("input.prompt"), input.prompt);
            }
        foreach (var binding in project.screens.Concat(project.themes.SelectMany(static theme => theme.screens)))
            foreach (var row in ScreenEditService.Flatten(binding.screen ?? throw new InvalidOperationException("An authored system screen is missing.")))
            {
                if (row.element.textId.Length > 0 && row.element.text.Length > 0) Add(row.element.textId, row.element.text);
                if (row.element.tooltipTextId.Length > 0 && row.element.tooltip.Length > 0) Add(row.element.tooltipTextId, row.element.tooltip);
                foreach (var option in row.element.options) if (option.textId.Length > 0) Add(option.textId, option.text);
            }
        return rows.OrderBy(static item => item.Key, StringComparer.Ordinal).Select(static item => new LocalizedText { id = item.Key, source = item.Value }).ToArray();

        void Add(string id, string source)
        {
            if (rows.TryGetValue(id, out string? previous) && previous != source) throw new InvalidOperationException("A text identity has conflicting sources: " + id);
            rows[id] = source;
        }
    }
}

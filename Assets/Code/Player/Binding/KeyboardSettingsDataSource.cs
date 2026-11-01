using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Input;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Player.Shell;
using Inno.VisualNovel.Player.Shell.Keyboard;
using Inno.VisualNovel.UI.Binding;

namespace Inno.VisualNovel.Player.Binding;

/// <summary>Exposes reading and menu mappings as separately owned typed rows, resolving current preferences on every query.</summary>
/// <param name="preferences">Returns current settings after any candidate profile replacement.</param>
/// <param name="localize">Resolves stable keyboard labels through the active game language.</param>
public sealed class KeyboardSettingsDataSource(
    Func<PlayerPreferences> preferences,
    Func<string, string, string> localize
) : UiDataSource
{
    /// <inheritdoc />
    public override bool TryRead(string path, out NovelValue value)
    { value = NovelValue.Enum(preferences().holdSkipModifiers.ToString()); return path == "settings.holdSkipModifiers"; }
    /// <inheritdoc />
    public override bool TryWrite(string path, NovelValue value)
    {
        if (path != "settings.holdSkipModifiers" || value.kind != NovelValueKind.Enum) return false;
        foreach (var modifier in NovelKeyboardShortcuts.modifiers)
            if (value.text == modifier.ToString()) { preferences().holdSkipModifiers = modifier; return true; }
        return false;
    }
    /// <inheritdoc />
    public override IReadOnlyList<UiDataRow> ReadRows(string path)
    {
        if (path is "settings.shortcutKeys" or "settings.navigationKeys") return (path == "settings.shortcutKeys" ? NovelKeyboardShortcuts.choices : NovelNavigationShortcuts.choices)
            .Select(choice => new UiDataRow(choice.id, new Dictionary<string, NovelValue>
        { ["text"] = NovelValue.String(choice.key == KeyCode.Unknown ? localize("keyboard/unassigned", "未设置") : choice.title), ["value"] = NovelValue.Enum(choice.id) })).ToArray();
        if (path == "settings.holdSkipChoices") return NovelKeyboardShortcuts.modifiers.Select(modifier => new UiDataRow(modifier.ToString(), new Dictionary<string, NovelValue>
        { ["text"] = NovelValue.String(modifier == KeyModifier.None ? localize("keyboard/unassigned", "未设置") : NovelKeyboardShortcuts.ModifierCaption(modifier)), ["value"] = NovelValue.Enum(modifier.ToString()) })).ToArray();
        if (path is not ("settings.shortcuts" or "settings.navigationShortcuts")) return [];
        bool navigation = path == "settings.navigationShortcuts";
        var bindings = (navigation ? preferences().navigationShortcuts : preferences().keyboardShortcuts).ToDictionary(static item => item.action, StringComparer.Ordinal);
        return (navigation ? NovelNavigationShortcuts.entries : NovelKeyboardShortcuts.entries).Select(entry => new UiDataRow(entry.action, new Dictionary<string, NovelValue>
        { ["name"] = NovelValue.String(localize("keyboard/" + (navigation ? "navigation/" : "") + entry.action, entry.title)),
            ["shortcutKey"] = NovelValue.Enum(NovelKeyboardShortcuts.ChoiceId(bindings[entry.action].key, bindings[entry.action].modifiers)) }, path)).ToArray();
    }
    /// <inheritdoc />
    public override bool TryWrite(
        string path,
        NovelValue value,
        UiDataRow? row
    ) {
        if (path != "item.shortcutKey" || row?.sourcePath is not ("settings.shortcuts" or "settings.navigationShortcuts") || value.kind != NovelValueKind.Enum) return false;
        bool navigation = row.sourcePath == "settings.navigationShortcuts";
        var choice = (navigation ? NovelNavigationShortcuts.choices : NovelKeyboardShortcuts.choices).FirstOrDefault(item => item.id == value.text);
        if (choice.id is null) return false;
        try
        {
            if (navigation) preferences().SetNavigationShortcut(row.key, choice.key, choice.modifiers);
            else preferences().SetKeyboardShortcut(row.key, choice.key, choice.modifiers);
            return true;
        }
        catch (ArgumentException failure)
        { throw new InvalidOperationException(localize("keyboard/conflict", "快捷键组合已被占用或不能用于此操作。请先更换另一项，或选择未设置。"), failure); }
    }
}

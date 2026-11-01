using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Input;

namespace Inno.VisualNovel.Player.Shell.Keyboard;

internal static class NovelKeyboardBindings
{
    internal static IReadOnlyList<(string id, string title, KeyCode key, KeyModifier modifiers)> Choices(IReadOnlyList<KeyCode> keys) => Array.AsReadOnly(
        new[] { (KeyCode.Unknown.ToString(), string.Empty, KeyCode.Unknown, KeyModifier.None) }.Concat(NovelKeyboardShortcuts.modifiers.SelectMany(modifier => keys.Where(static key => key != KeyCode.Unknown)
            .Select(key => (NovelKeyboardShortcuts.ChoiceId(key, modifier), NovelKeyboardShortcuts.Caption(key, modifier), key, modifier)))).ToArray());

    internal static void Validate(
        IReadOnlyList<NovelKeyboardBinding> bindings,
        IReadOnlyList<(string action, string title, KeyCode key)> entries,
        IReadOnlyList<(string id, string title, KeyCode key, KeyModifier modifiers)> choices
    ) {
        ArgumentNullException.ThrowIfNull(bindings);
        if (bindings.Count != entries.Count || bindings.Any(item => item is null || !entries.Any(entry => entry.action == item.action)
                || !choices.Any(choice => choice.key == item.key && choice.modifiers == item.modifiers))
            || bindings.Select(static item => item.action).Distinct(StringComparer.Ordinal).Count() != bindings.Count
            || bindings.Where(static item => item.key != KeyCode.Unknown).GroupBy(static item => (item.key, item.modifiers)).Any(static group => group.Count() > 1))
            throw new ArgumentException("Keyboard mappings require every supported action exactly once, available chords and no duplicate enabled combinations.", nameof(bindings));
    }
    internal static void Distinct(
        IReadOnlyList<NovelKeyboardBinding> reading,
        IReadOnlyList<NovelKeyboardBinding> navigation
    ) {
        if (reading.Concat(navigation).Where(static item => item.key != KeyCode.Unknown).GroupBy(static item => (item.key, item.modifiers)).Any(static group => group.Count() > 1))
            throw new ArgumentException("Reading and navigation actions cannot share the same enabled key combination.");
    }
    internal static string Resolve(
        IReadOnlyCollection<KeyCode> pressed,
        IReadOnlyList<NovelKeyboardBinding>? bindings,
        KeyModifier modifier,
        IReadOnlyList<(string action, string title, KeyCode key)> entries
    ) {
        foreach (var entry in entries.Reverse())
        {
            var binding = bindings?.Single(item => item.action == entry.action);
            KeyCode key = binding?.key ?? entry.key; KeyModifier required = binding?.modifiers ?? KeyModifier.None;
            if (key != KeyCode.Unknown && modifier == required && pressed.Contains(key)) return entry.action;
        }
        return string.Empty;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Input;

namespace Inno.VisualNovel.Player.Shell.Keyboard;

/// <summary>Defines supported reading shortcuts, bounded key choices and deterministic simultaneous-press priority.</summary>
public static class NovelKeyboardShortcuts
{
    /// <summary>Gets action identities, localizable fallback titles and defaults in display order; simultaneous presses use the reverse order.</summary>
    public static IReadOnlyList<(string action, string title, KeyCode key)> entries { get; } = Array.AsReadOnly(new[]
    {
        ("automatic", "切换自动播放", KeyCode.A), ("skip", "切换快进", KeyCode.S), ("hide", "隐藏阅读界面", KeyCode.H),
        ("history", "打开历史记录", KeyCode.L), ("rollback", "回滚阅读", KeyCode.R), ("quickSave", "快速存档", KeyCode.F5), ("quickLoad", "快速读档", KeyCode.F9)
    });
    /// <summary>Gets supported letter/function keys plus Unknown to disable a shortcut; essential navigation and text-editing keys remain available.</summary>
    public static IReadOnlyList<KeyCode> keys { get; } = Array.AsReadOnly(new[] { KeyCode.Unknown }.Concat(Enum.GetValues<KeyCode>()
        .Where(static key => (int)key is >= (int)KeyCode.A and <= (int)KeyCode.Z or >= (int)KeyCode.F1 and <= (int)KeyCode.F12)).ToArray());
    /// <summary>Gets supported ordinary modifier combinations, including None for an unmodified shortcut or disabled hold-to-skip.</summary>
    public static IReadOnlyList<KeyModifier> modifiers { get; } = Array.AsReadOnly(new[] { KeyModifier.None, KeyModifier.Control, KeyModifier.Shift, KeyModifier.Alt,
        KeyModifier.Control | KeyModifier.Shift, KeyModifier.Alt | KeyModifier.Shift });
    /// <summary>Gets bounded atomic selector choices combining each supported key and modifier policy.</summary>
    public static IReadOnlyList<(string id, string title, KeyCode key, KeyModifier modifiers)> choices { get; } = NovelKeyboardBindings.Choices(keys);
    /// <summary>Gets the stable selector identity for an exact supported combination.</summary>
    /// <param name="key">The formal key.</param>
    /// <param name="modifier">The exact modifier combination.</param>
    /// <returns>A neutral choice ID, with unmodified keys retaining their normal key names.</returns>
    public static string ChoiceId(
        KeyCode key,
        KeyModifier modifier
    ) => modifier == KeyModifier.None ? key.ToString() : modifier.ToString().Replace(", ", "+", StringComparison.Ordinal) + "+" + key;
    /// <summary>Formats standard key names without depending on process culture or game state.</summary>
    /// <param name="key">The formal key.</param>
    /// <param name="modifier">The exact modifiers.</param>
    /// <returns>A short human-readable key combination.</returns>
    public static string Caption(
        KeyCode key,
        KeyModifier modifier
    ) => (modifier == KeyModifier.None ? string.Empty : ModifierCaption(modifier) + "+") + key;
    /// <summary>Formats the supported modifier portion of a binding independently of physical keys.</summary>
    /// <param name="modifier">The exact modifier combination.</param>
    /// <returns>Standard modifier names joined by plus signs, or empty for None.</returns>
    public static string ModifierCaption(KeyModifier modifier) => string.Join("+", new[] { modifier.HasFlag(KeyModifier.Control) ? "Ctrl" : string.Empty,
        modifier.HasFlag(KeyModifier.Alt) ? "Alt" : string.Empty, modifier.HasFlag(KeyModifier.Shift) ? "Shift" : string.Empty }.Where(static item => item.Length > 0));
    /// <summary>Creates independent default mappings, avoiding shared mutable preference arrays.</summary>
    /// <returns>One fresh declaration for every supported action.</returns>
    public static NovelKeyboardBinding[] CreateDefaults() => entries.Select(static entry => new NovelKeyboardBinding { action = entry.action, key = entry.key }).ToArray();
    /// <summary>Validates a complete mapping before committing any player preference changes.</summary>
    /// <param name="bindings">Exactly one declared mapping for each supported action.</param>
    public static void Validate(IReadOnlyList<NovelKeyboardBinding> bindings) => NovelKeyboardBindings.Validate(bindings, entries, choices);
    /// <summary>Resolves at most one reading action against default mappings in stable priority.</summary>
    /// <param name="pressed">The viewport's single-frame formal key presses.</param>
    /// <param name="modifier">The viewport's exact current modifier combination.</param>
    /// <returns>A supported action identity, or empty when no default shortcut was pressed.</returns>
    public static string ResolveDefault(
        IReadOnlyCollection<KeyCode> pressed,
        KeyModifier modifier = KeyModifier.None
    ) => Resolve(pressed, null, modifier);

    internal static string Resolve(
        IReadOnlyCollection<KeyCode> pressed,
        IReadOnlyList<NovelKeyboardBinding>? bindings,
        KeyModifier modifier
    ) => NovelKeyboardBindings.Resolve(pressed, bindings, modifier, entries);
}

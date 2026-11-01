using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Input;

namespace Inno.VisualNovel.Player.Shell.Keyboard;

/// <summary>Defines configurable game-menu actions independently of physical text-editing gestures.</summary>
public static class NovelNavigationShortcuts
{
    /// <summary>Gets stable navigation actions and defaults; reverse display order determines simultaneous-press priority.</summary>
    public static IReadOnlyList<(string action, string title, KeyCode key)> entries { get; } = Array.AsReadOnly(new[]
    {
        ("confirm", "确认操作", KeyCode.Enter), ("next", "下一项", KeyCode.DownArrow), ("previous", "上一项", KeyCode.UpArrow),
        ("increase", "增加数值 / 向右", KeyCode.RightArrow), ("decrease", "减少数值 / 向左", KeyCode.LeftArrow),
        ("pageUp", "向上翻页", KeyCode.PageUp), ("pageDown", "向下翻页", KeyCode.PageDown), ("cancel", "返回 / 取消", KeyCode.Escape)
    });
    /// <summary>Gets navigation keys, ordinary letters and function keys; Tab and text-editing keys retain their physical roles.</summary>
    public static IReadOnlyList<KeyCode> keys { get; } = Array.AsReadOnly(NovelKeyboardShortcuts.keys.Concat(entries.Select(static entry => entry.key)).Distinct().ToArray());
    /// <summary>Gets bounded atomic navigation choices including explicit disablement.</summary>
    public static IReadOnlyList<(string id, string title, KeyCode key, KeyModifier modifiers)> choices { get; } = NovelKeyboardBindings.Choices(keys);
    /// <summary>Creates fresh independent default declarations for every navigation action.</summary>
    /// <returns>A complete owned table.</returns>
    public static NovelKeyboardBinding[] CreateDefaults() => entries.Select(static entry => new NovelKeyboardBinding { action = entry.action, key = entry.key }).ToArray();
    /// <summary>Rejects incomplete tables, unsupported combinations and duplicate enabled chords.</summary>
    /// <param name="bindings">The complete proposed navigation policy.</param>
    public static void Validate(IReadOnlyList<NovelKeyboardBinding> bindings) => NovelKeyboardBindings.Validate(bindings, entries, choices);
    /// <summary>Resolves one default menu action using exact current modifiers.</summary>
    /// <param name="pressed">Viewport-local single-frame key presses.</param>
    /// <param name="modifiers">Exact current modifiers.</param>
    /// <returns>One stable navigation action, or empty.</returns>
    public static string ResolveDefault(
        IReadOnlyCollection<KeyCode> pressed,
        KeyModifier modifiers = KeyModifier.None
    ) => Resolve(pressed, null, modifiers);
    internal static string Resolve(
        IReadOnlyCollection<KeyCode> pressed,
        IReadOnlyList<NovelKeyboardBinding>? bindings,
        KeyModifier modifiers
    ) => NovelKeyboardBindings.Resolve(pressed, bindings, modifiers, entries);
}

using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Text;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.UI.Theming;

namespace Inno.VisualNovel.Authoring.Templates;

/// <summary>Creates complete editable shell families with shared tokens, distinct composition and accessible content sizing.</summary>
public static class ShellThemeTemplates
{
    private static readonly IReadOnlyList<string> m_names = Array.AsReadOnly(new[] { "Clear", "Paper", "Cinema" });
    private static readonly IReadOnlyList<string> m_roles = Array.AsReadOnly(new[] { "loading", "title", "reading", "pause", "settings", "history", "credits", "save", "load", "confirm",
        "extras", "gallery", "music", "replays", "endings", "glossary", "characters", "collectionDetail" });
    /// <summary>Gets the three supported built-in theme families.</summary>
    public static IReadOnlyList<string> names => m_names;
    /// <summary>Gets every implemented editable system page covered by each theme.</summary>
    public static IReadOnlyList<string> roles => m_roles;
    /// <summary>Creates independent style declarations and all system pages before native asset installation.</summary>
    /// <param name="name">Clear, Paper or Cinema.</param>
    /// <param name="font">The native imported default font.</param>
    /// <returns>A detached theme whose pages must be saved individually and rebound before saving the theme.</returns>
    public static ThemeAsset Create(
        string name,
        FontAsset font
    ) {
        if (!names.Contains(name, StringComparer.Ordinal)) throw new ArgumentException("Unknown built-in theme: " + name, nameof(name));
        var palette = name switch
        {
            "Paper" => new uint[] { 0xEEE5D5FF, 0xFFF9EDF8, 0xF8F0DFFF, 0xE8D8BBFF, 0xD5BE98FF, 0xE6DFD3FF, 0x453C35FF, 0x75634DFF, 0x976548FF },
            "Cinema" => new uint[] { 0x101318FF, 0x121820F2, 0x232B35FF, 0x35404AFF, 0x445663FF, 0x25282EFF, 0xF2F0EAFF, 0xA2A5ACFF, 0xD8B77AFF },
            _ => [0x141C2AFF, 0x233149F5, 0x2B3D56FF, 0x3B5671FF, 0x4C6D8CFF, 0x252F3EFF, 0xF2F5FCFF, 0xB8C4D4FF, 0xA5D6F1FF]
        };
        string[] keys = ["surface.background", "surface.panel", "control.normal", "control.hover", "control.pressed", "control.disabled", "text.primary", "text.muted", "accent"];
        var theme = new ThemeAsset
        {
            resourceId = "builtin.theme." + name.ToLowerInvariant(), title = name,
            description = name == "Paper" ? "温暖的纸页与书信式构图。" : name == "Cinema" ? "电影字幕、宽屏留白与克制的金色强调。" : "现代清晰的阅读层级与蓝色焦点。",
            font = font, colors = keys.Select((key, index) => new ThemeColorToken { key = key, value = palette[index] }).ToArray(),
            metrics = [new() { key = "font.body", value = 28 }, new() { key = "font.caption", value = 24 }, new() { key = "font.heading", value = name == "Paper" ? 48 : 52 },
                new() { key = "font.title", value = name == "Cinema" ? 84 : 76 }, new() { key = "font.reading", value = name == "Paper" ? 34 : 32 },
                new() { key = "font.option", value = 30 }, new() { key = "line.body", value = name == "Paper" ? 1.55f : 1.35f }, new() { key = "padding.control", value = 18 }]
        };
        theme.styles = [
            Style("body", "", UiStyleProperty.All, new() { background = 0, hover = 0, pressed = 0, disabled = 0, textPadding = 0 },
                (UiStyleProperty.Foreground, "text.primary"), (UiStyleProperty.DisabledForeground, "text.muted"), (UiStyleProperty.Focus, "accent"), (UiStyleProperty.FontSize, "font.body"), (UiStyleProperty.LineSpacing, "line.body")),
            Style("caption", "body", UiStyleProperty.FontSize | UiStyleProperty.Foreground, new(), (UiStyleProperty.FontSize, "font.caption"), (UiStyleProperty.Foreground, "text.muted")),
            Style("heading", "body", UiStyleProperty.FontSize, new(), (UiStyleProperty.FontSize, "font.heading")),
            Style("title", "heading", UiStyleProperty.FontSize, new(), (UiStyleProperty.FontSize, "font.title")),
            Style("accent.text", "body", UiStyleProperty.Foreground, new(), (UiStyleProperty.Foreground, "accent")),
            Style("reading", "body", UiStyleProperty.FontSize | UiStyleProperty.LineSpacing, new() { lineSpacing = name == "Cinema" ? 1.5f : 1.6f }, (UiStyleProperty.FontSize, "font.reading")),
            Style("panel", "body", UiStyleProperty.Background, new(), (UiStyleProperty.Background, "surface.panel")),
            Style("screen", "panel", UiStyleProperty.Background, new(), (UiStyleProperty.Background, "surface.background")),
            Style("accent.fill", "body", UiStyleProperty.Background, new(), (UiStyleProperty.Background, "accent")),
            Style("button", "body", UiStyleProperty.Background | UiStyleProperty.Hover | UiStyleProperty.Pressed | UiStyleProperty.Disabled | UiStyleProperty.TextPadding, new(),
                (UiStyleProperty.Background, "control.normal"), (UiStyleProperty.Hover, "control.hover"), (UiStyleProperty.Pressed, "control.pressed"),
                (UiStyleProperty.Disabled, "control.disabled"), (UiStyleProperty.TextPadding, "padding.control")),
            Style("option.text", "body", UiStyleProperty.FontSize, new(), (UiStyleProperty.FontSize, "font.option")) ];
        theme.screens = roles.Select(role => new NovelScreenBinding { role = role, screen = ThemeScreenComposition.Create(role, font, name, theme) }).ToArray();
        _ = new UiThemeResolver(theme);
        return theme;
    }
    private static ThemeStyle Style(
        string key,
        string parent,
        UiStyleProperty fields,
        UiStyleDefinition values,
        params (UiStyleProperty property, string token)[] bindings
    ) => new() { key = key, parent = parent, fields = fields, values = values,
        bindings = bindings.Select(static binding => new ThemeStyleBinding { property = binding.property, token = binding.token }).ToArray() };
}

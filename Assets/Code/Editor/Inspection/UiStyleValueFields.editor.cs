using System;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.UI.Theming;

namespace Inno.VisualNovel.Editor.Inspection;

internal static class UiStyleValueFields
{
    internal static string Label(UiStyleProperty property) => property switch
    {
        UiStyleProperty.Background => "Normal background", UiStyleProperty.Hover => "Hover background", UiStyleProperty.Pressed => "Pressed background",
        UiStyleProperty.Disabled => "Disabled background", UiStyleProperty.Foreground => "Text color", UiStyleProperty.DisabledForeground => "Disabled text",
        UiStyleProperty.ImageTint => "Image tint", UiStyleProperty.Focus => "Focus border", UiStyleProperty.FontSize => "Font size",
        UiStyleProperty.LineSpacing => "Line spacing multiplier", UiStyleProperty.TextPadding => "Text padding", UiStyleProperty.Opacity => "Opacity",
        UiStyleProperty.Alignment => "Text alignment", _ => throw new ArgumentOutOfRangeException(nameof(property))
    };
    internal static UiStyleProperty Draw(
        StoryInspectorFields fields,
        string id,
        UiStyleDefinition style,
        UiStyleProperty enabled
    ) {
        UiStyleProperty changed = UiStyleProperty.None;
        foreach (var property in UiStyleFields.properties)
            if (enabled.HasFlag(property) && DrawField(fields, id, style, property)) changed |= property;
        return changed;
    }
    internal static bool DrawField(
        StoryInspectorFields fields,
        string id,
        UiStyleDefinition style,
        UiStyleProperty property
    ) {
        id += "/" + property; string label = Label(property);
        if (UiStyleFields.IsColor(property))
        {
            uint value = property switch
            {
                UiStyleProperty.Background => style.background, UiStyleProperty.Hover => style.hover, UiStyleProperty.Pressed => style.pressed,
                UiStyleProperty.Disabled => style.disabled, UiStyleProperty.Foreground => style.foreground, UiStyleProperty.DisabledForeground => style.disabledForeground,
                UiStyleProperty.ImageTint => style.imageTint, UiStyleProperty.Focus => style.focus, _ => throw new ArgumentOutOfRangeException(nameof(property))
            };
            if (!fields.Color(id, label, ref value)) return false;
            switch (property)
            {
                case UiStyleProperty.Background: style.background = value; break;
                case UiStyleProperty.Hover: style.hover = value; break;
                case UiStyleProperty.Pressed: style.pressed = value; break;
                case UiStyleProperty.Disabled: style.disabled = value; break;
                case UiStyleProperty.Foreground: style.foreground = value; break;
                case UiStyleProperty.DisabledForeground: style.disabledForeground = value; break;
                case UiStyleProperty.ImageTint: style.imageTint = value; break;
                case UiStyleProperty.Focus: style.focus = value; break;
            }
            return true;
        }
        if (property == UiStyleProperty.Alignment)
        { var alignment = style.alignment; bool changed = fields.SelectEnum(id, label, ref alignment); style.alignment = alignment; return changed; }
        double number = property switch
        {
            UiStyleProperty.FontSize => style.fontSize, UiStyleProperty.LineSpacing => style.lineSpacing,
            UiStyleProperty.TextPadding => style.textPadding, UiStyleProperty.Opacity => style.opacity, _ => throw new ArgumentOutOfRangeException(nameof(property))
        };
        double minimum = property == UiStyleProperty.FontSize ? 1 : property == UiStyleProperty.LineSpacing ? 0.5 : 0;
        double maximum = property == UiStyleProperty.Opacity ? 1 : property == UiStyleProperty.LineSpacing ? 5 : 100000;
        if (!fields.Number(id, label, ref number, minimum, maximum)) return false;
        switch (property)
        {
            case UiStyleProperty.FontSize: style.fontSize = (float)number; break;
            case UiStyleProperty.LineSpacing: style.lineSpacing = (float)number; break;
            case UiStyleProperty.TextPadding: style.textPadding = (float)number; break;
            case UiStyleProperty.Opacity: style.opacity = (float)number; break;
        }
        return true;
    }
}

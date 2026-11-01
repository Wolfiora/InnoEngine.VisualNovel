using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Interface.Themes;

namespace Inno.VisualNovel.UI.Theming;

/// <summary>Shares explicit style field ownership and token types without reflection or property-name execution.</summary>
public static class UiStyleFields
{
    private static readonly IReadOnlyList<UiStyleProperty> m_properties = Array.AsReadOnly(Enum.GetValues<UiStyleProperty>().Where(static property => IsSingle(property)).ToArray());
    /// <summary>Gets all individually editable style fields in stable declaration order.</summary>
    public static IReadOnlyList<UiStyleProperty> properties => m_properties;
    /// <summary>Reports whether a single field accepts a packed color token.</summary>
    /// <param name="property">The exact style field.</param>
    /// <returns>Whether its value is a packed color.</returns>
    public static bool IsColor(UiStyleProperty property) => IsSingle(property) && property <= UiStyleProperty.Focus;
    /// <summary>Reports whether a single field accepts a numeric metric token.</summary>
    /// <param name="property">The exact style field.</param>
    /// <returns>Whether its value is a numeric metric.</returns>
    public static bool IsMetric(UiStyleProperty property) => property is UiStyleProperty.FontSize or UiStyleProperty.LineSpacing or UiStyleProperty.TextPadding or UiStyleProperty.Opacity;
    /// <summary>Copies only declared fields, leaving all other inherited values intact.</summary>
    /// <param name="target">The independently owned destination style.</param>
    /// <param name="source">The source literal values.</param>
    /// <param name="fields">The explicit field mask.</param>
    public static void Apply(
        UiStyleDefinition target,
        UiStyleDefinition source,
        UiStyleProperty fields
    ) {
        if ((fields & ~UiStyleProperty.All) != 0) throw new InvalidOperationException("Style override contains an unknown field.");
        if (fields.HasFlag(UiStyleProperty.Background)) target.background = source.background;
        if (fields.HasFlag(UiStyleProperty.Hover)) target.hover = source.hover;
        if (fields.HasFlag(UiStyleProperty.Pressed)) target.pressed = source.pressed;
        if (fields.HasFlag(UiStyleProperty.Disabled)) target.disabled = source.disabled;
        if (fields.HasFlag(UiStyleProperty.Foreground)) target.foreground = source.foreground;
        if (fields.HasFlag(UiStyleProperty.DisabledForeground)) target.disabledForeground = source.disabledForeground;
        if (fields.HasFlag(UiStyleProperty.ImageTint)) target.imageTint = source.imageTint;
        if (fields.HasFlag(UiStyleProperty.Focus)) target.focus = source.focus;
        if (fields.HasFlag(UiStyleProperty.FontSize)) target.fontSize = source.fontSize;
        if (fields.HasFlag(UiStyleProperty.LineSpacing)) target.lineSpacing = source.lineSpacing;
        if (fields.HasFlag(UiStyleProperty.TextPadding)) target.textPadding = source.textPadding;
        if (fields.HasFlag(UiStyleProperty.Opacity)) target.opacity = source.opacity;
        if (fields.HasFlag(UiStyleProperty.Alignment)) target.alignment = source.alignment;
    }
    internal static void Color(
        UiStyleDefinition target,
        UiStyleProperty property,
        uint value
    ) {
        switch (property)
        {
            case UiStyleProperty.Background: target.background = value; break;
            case UiStyleProperty.Hover: target.hover = value; break;
            case UiStyleProperty.Pressed: target.pressed = value; break;
            case UiStyleProperty.Disabled: target.disabled = value; break;
            case UiStyleProperty.Foreground: target.foreground = value; break;
            case UiStyleProperty.DisabledForeground: target.disabledForeground = value; break;
            case UiStyleProperty.ImageTint: target.imageTint = value; break;
            case UiStyleProperty.Focus: target.focus = value; break;
            default: throw new InvalidOperationException("This style field does not accept a color token.");
        }
    }
    internal static void Metric(
        UiStyleDefinition target,
        UiStyleProperty property,
        float value
    ) {
        switch (property)
        {
            case UiStyleProperty.FontSize: target.fontSize = value; break;
            case UiStyleProperty.LineSpacing: target.lineSpacing = value; break;
            case UiStyleProperty.TextPadding: target.textPadding = value; break;
            case UiStyleProperty.Opacity: target.opacity = value; break;
            default: throw new InvalidOperationException("This style field does not accept a metric token.");
        }
    }
    internal static void Validate(UiStyleDefinition style)
    {
        if (!float.IsFinite(style.fontSize) || style.fontSize <= 0 || !float.IsFinite(style.lineSpacing) || style.lineSpacing is < 0.5f or > 5
            || !float.IsFinite(style.textPadding) || style.textPadding < 0 || !float.IsFinite(style.opacity) || style.opacity is < 0 or > 1 || !Enum.IsDefined(style.alignment))
            throw new InvalidOperationException("Resolved theme style has invalid typography, spacing, alignment or opacity.");
    }
    private static bool IsSingle(UiStyleProperty property) => property > UiStyleProperty.None && (property & ~UiStyleProperty.All) == 0 && ((int)property & ((int)property - 1)) == 0;
}

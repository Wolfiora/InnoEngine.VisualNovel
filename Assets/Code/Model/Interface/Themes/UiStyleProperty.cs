using System;

namespace Inno.VisualNovel.Model.Interface.Themes;

/// <summary>Selects explicit style fields for inheritance, token binding and page-local overrides.</summary>
[Flags]
public enum UiStyleProperty
{
    /// <summary>Overrides no fields.</summary>
    None = 0,
    /// <summary>The normal fill.</summary>
    Background = 1 << 0,
    /// <summary>The hovered fill.</summary>
    Hover = 1 << 1,
    /// <summary>The pressed fill.</summary>
    Pressed = 1 << 2,
    /// <summary>The disabled fill.</summary>
    Disabled = 1 << 3,
    /// <summary>The normal text color.</summary>
    Foreground = 1 << 4,
    /// <summary>The disabled text color.</summary>
    DisabledForeground = 1 << 5,
    /// <summary>The image tint.</summary>
    ImageTint = 1 << 6,
    /// <summary>The keyboard focus outline.</summary>
    Focus = 1 << 7,
    /// <summary>The logical font size.</summary>
    FontSize = 1 << 8,
    /// <summary>The line-height multiplier.</summary>
    LineSpacing = 1 << 9,
    /// <summary>The text inset.</summary>
    TextPadding = 1 << 10,
    /// <summary>The complete element opacity.</summary>
    Opacity = 1 << 11,
    /// <summary>The wrapped-line alignment.</summary>
    Alignment = 1 << 12,
    /// <summary>Overrides every supported style field.</summary>
    All = (1 << 13) - 1
}

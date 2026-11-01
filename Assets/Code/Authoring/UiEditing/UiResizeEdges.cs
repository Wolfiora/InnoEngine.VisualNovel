using System;

namespace Inno.VisualNovel.Authoring.UiEditing;

/// <summary>Selects canvas edges to resize; None translates the complete element.</summary>
[Flags]
public enum UiResizeEdges
{
    /// <summary>Moves the element without changing its measured size.</summary>
    None = 0,
    /// <summary>Moves the left edge, preserving the right edge.</summary>
    Left = 1,
    /// <summary>Moves the upper edge, preserving the lower edge.</summary>
    Top = 2,
    /// <summary>Moves the right edge, preserving the left edge.</summary>
    Right = 4,
    /// <summary>Moves the lower edge, preserving the upper edge.</summary>
    Bottom = 8
}

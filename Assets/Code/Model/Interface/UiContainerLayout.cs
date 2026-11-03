namespace Inno.VisualNovel.Model.Interface;

/// <summary>Chooses how a native UI container allocates slots to its actual children.</summary>
public enum UiContainerLayout
{
    /// <summary>Uses child anchors and native Transform offsets.</summary>
    Absolute,
    /// <summary>Allocates horizontal slots in native sibling order.</summary>
    Horizontal,
    /// <summary>Allocates vertical slots in native sibling order.</summary>
    Vertical,
    /// <summary>Allocates equal-width grid cells in native sibling order.</summary>
    Grid
}

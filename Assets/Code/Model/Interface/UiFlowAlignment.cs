namespace Inno.VisualNovel.Model.Interface;

/// <summary>Distributes unused main-axis space in a horizontal or vertical container after measuring children, margins and gaps.</summary>
public enum UiFlowAlignment
{
    /// <summary>Places the group at its leading edge.</summary>
    Start,
    /// <summary>Centers the complete group.</summary>
    Center,
    /// <summary>Places the group at its trailing edge.</summary>
    End,
    /// <summary>Adds equal unused space between children while retaining the authored minimum gap.</summary>
    SpaceBetween,
    /// <summary>Adds equal space around children, with half that amount at the two outer edges.</summary>
    SpaceAround
}

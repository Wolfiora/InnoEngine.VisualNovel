namespace Inno.VisualNovel.Model.Stages;

/// <summary>Defines linear-light composition of one isolated stage image over its backdrop.</summary>
public enum StageBlendMode
{
    /// <summary>Composites source coverage over the destination.</summary>
    Normal,
    /// <summary>Adds premultiplied source light and coverage with saturation at one.</summary>
    Add,
    /// <summary>Multiplies overlapping straight colors and preserves uncovered regions.</summary>
    Multiply
}

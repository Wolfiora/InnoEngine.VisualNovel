namespace Inno.VisualNovel.Model.Stages;

/// <summary>Defines coverage sampled from a mask aligned to the complete uncropped local image.</summary>
public enum StageMaskMode
{
    /// <summary>Uses mask alpha as coverage.</summary>
    Alpha,
    /// <summary>Uses linear luminance multiplied by mask alpha.</summary>
    Luminance,
    /// <summary>Uses one minus mask alpha.</summary>
    InverseAlpha,
    /// <summary>Uses one minus alpha-weighted linear luminance.</summary>
    InverseLuminance
}

using System;

namespace Inno.VisualNovel.Model.Performance;

/// <summary>Shares finite scalar bounds and usable defaults between authoring and playback validation.</summary>
public static class PerformancePropertyRules
{
    /// <summary>Gets inclusive authoring and runtime limits for a known scalar channel.</summary>
    /// <param name="property">The declared channel.</param>
    /// <returns>Finite minimum and maximum values.</returns>
    public static (double minimum, double maximum) Range(PerformanceProperty property) => property switch
    {
        PerformanceProperty.Opacity or PerformanceProperty.Volume or PerformanceProperty.EffectStrength or PerformanceProperty.VignetteRadius => (0, 1),
        PerformanceProperty.BlurRadius or PerformanceProperty.AberrationPixels => (0, 32),
        PerformanceProperty.VignetteSoftness => (.001, 1),
        PerformanceProperty.Saturation or PerformanceProperty.Contrast => (0, 4),
        PerformanceProperty.Brightness => (-1, 1),
        PerformanceProperty.PixelSize => (1, 512),
        PerformanceProperty.LetterboxRatio => (0, .45),
        PerformanceProperty.X or PerformanceProperty.Y or PerformanceProperty.ScaleX or PerformanceProperty.ScaleY
            or PerformanceProperty.Rotation or PerformanceProperty.ShakeX or PerformanceProperty.ShakeY => (-1000000, 1000000),
        _ => throw new ArgumentOutOfRangeException(nameof(property))
    };
    /// <summary>Checks one scalar against the complete property contract.</summary>
    /// <param name="property">The declared channel.</param>
    /// <param name="value">The proposed scalar.</param>
    /// <returns>Whether it is finite and within the channel's inclusive limits.</returns>
    public static bool IsValid(
        PerformanceProperty property,
        double value
    ) {
        if (!Enum.IsDefined(property)) return false;
        var range = Range(property); return double.IsFinite(value) && value >= range.minimum && value <= range.maximum;
    }
    /// <summary>Gets a usable initial key value that satisfies the declared channel's bounds.</summary>
    /// <param name="property">The channel selected in a timeline.</param>
    /// <returns>The ordinary default value for that channel.</returns>
    public static double Default(PerformanceProperty property) => property switch
    {
        PerformanceProperty.ScaleX or PerformanceProperty.ScaleY or PerformanceProperty.Opacity or PerformanceProperty.Volume
            or PerformanceProperty.EffectStrength or PerformanceProperty.Saturation or PerformanceProperty.Contrast => 1,
        PerformanceProperty.BlurRadius => 3, PerformanceProperty.VignetteRadius => .55, PerformanceProperty.VignetteSoftness => .4,
        PerformanceProperty.PixelSize => 8, PerformanceProperty.LetterboxRatio => .12, PerformanceProperty.AberrationPixels => 2,
        PerformanceProperty.X or PerformanceProperty.Y or PerformanceProperty.Rotation or PerformanceProperty.ShakeX or PerformanceProperty.ShakeY or PerformanceProperty.Brightness => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(property))
    };
}

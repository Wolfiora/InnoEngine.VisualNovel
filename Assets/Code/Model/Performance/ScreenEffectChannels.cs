using System;

namespace Inno.VisualNovel.Model.Performance;

/// <summary>Declares the actual scalar channels implemented by each screen-effect mechanism.</summary>
public static class ScreenEffectChannels
{
    /// <summary>Gets detached channel capabilities for a concrete image operation.</summary>
    /// <param name="kind">The preset's implemented effect kind.</param>
    /// <returns>Blend strength and the parameters meaningful for that mechanism.</returns>
    public static PerformanceProperty[] For(ScreenEffectKind kind) => kind switch
    {
        ScreenEffectKind.Blur => [PerformanceProperty.EffectStrength, PerformanceProperty.BlurRadius],
        ScreenEffectKind.Vignette => [PerformanceProperty.EffectStrength, PerformanceProperty.VignetteRadius, PerformanceProperty.VignetteSoftness],
        ScreenEffectKind.ColorGrade => [PerformanceProperty.EffectStrength, PerformanceProperty.Saturation, PerformanceProperty.Brightness, PerformanceProperty.Contrast],
        ScreenEffectKind.Pixelate => [PerformanceProperty.EffectStrength, PerformanceProperty.PixelSize],
        ScreenEffectKind.Letterbox => [PerformanceProperty.EffectStrength, PerformanceProperty.LetterboxRatio],
        ScreenEffectKind.ChromaticAberration => [PerformanceProperty.EffectStrength, PerformanceProperty.AberrationPixels],
        ScreenEffectKind.Grayscale or ScreenEffectKind.Flash or ScreenEffectKind.Sepia or ScreenEffectKind.Invert => [PerformanceProperty.EffectStrength],
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}

using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Performance;

/// <summary>Identifies a built-in scalar property animated by a stage track.</summary>
public enum PerformanceProperty
{
    /// <summary>Animates horizontal position.</summary>
    X,
    /// <summary>Animates vertical position.</summary>
    Y,
    /// <summary>Animates horizontal scale.</summary>
    ScaleX,
    /// <summary>Animates vertical scale.</summary>
    ScaleY,
    /// <summary>Animates rotation in degrees.</summary>
    Rotation,
    /// <summary>Animates opacity in [0, 1].</summary>
    Opacity,
    /// <summary>Adds horizontal shake independently of the main position writer.</summary>
    ShakeX,
    /// <summary>Adds vertical shake independently of the main position writer.</summary>
    ShakeY,
    /// <summary>Multiplies an explicitly supported audio slot's gain in [0, 1].</summary>
    Volume,
    /// <summary>Multiplies an active screen effect's blend strength in [0, 1].</summary>
    EffectStrength,
    /// <summary>Overrides a blur effect's radius in design pixels.</summary>
    BlurRadius,
    /// <summary>Overrides the normalized inner radius of a vignette.</summary>
    VignetteRadius,
    /// <summary>Overrides the normalized soft edge of a vignette.</summary>
    VignetteSoftness,
    /// <summary>Overrides color-grade saturation.</summary>
    Saturation,
    /// <summary>Overrides additive color-grade brightness.</summary>
    Brightness,
    /// <summary>Overrides color-grade contrast.</summary>
    Contrast,
    /// <summary>Overrides pixelation cell width in design pixels.</summary>
    PixelSize,
    /// <summary>Overrides the height fraction of each cinematic bar.</summary>
    LetterboxRatio,
    /// <summary>Overrides opposing color offsets in design pixels.</summary>
    AberrationPixels
}

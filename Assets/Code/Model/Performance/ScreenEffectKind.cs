namespace Inno.VisualNovel.Model.Performance;

/// <summary>Identifies the image operation evaluated by the VN post-processing shader.</summary>
public enum ScreenEffectKind
{
    /// <summary>A bounded Gaussian blur in design pixels.</summary>
    Blur = 0,
    /// <summary>Soft darkening around the image edges.</summary>
    Vignette = 1,
    /// <summary>Luminance-preserving removal of color.</summary>
    Grayscale = 2,
    /// <summary>Brightness, contrast and saturation adjustment.</summary>
    ColorGrade = 3,
    /// <summary>Nearest-cell sampling at an authored design-pixel size.</summary>
    Pixelate = 4,
    /// <summary>A tinted full-frame flash respecting player accessibility policy.</summary>
    Flash = 5,
    /// <summary>Top and bottom cinematic bars.</summary>
    Letterbox = 6,
    /// <summary>A warm three-channel sepia transform.</summary>
    Sepia = 7,
    /// <summary>Color inversion blended at the authored strength.</summary>
    Invert = 8,
    /// <summary>Bounded opposing red and blue sample offsets.</summary>
    ChromaticAberration = 9
}

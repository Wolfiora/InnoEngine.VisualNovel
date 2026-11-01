using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Performance;

/// <summary>Declares editable image-effect parameters independently of a graphics backend.</summary>
[GenerateSerializationConverter]
public sealed class ScreenEffectDefinition : ISerializable
{
    /// <summary>Gets or sets the image operation.</summary>
    [SerializableProperty] public ScreenEffectKind kind { get; set; }
    /// <summary>Gets or sets blur radius in design pixels.</summary>
    [SerializableProperty] public double blurRadius { get; set; } = 3;
    /// <summary>Gets or sets the vignette's normalized inner radius.</summary>
    [SerializableProperty] public double vignetteRadius { get; set; } = 0.55;
    /// <summary>Gets or sets the vignette's normalized soft edge.</summary>
    [SerializableProperty] public double vignetteSoftness { get; set; } = 0.4;
    /// <summary>Gets or sets the saturation multiplier.</summary>
    [SerializableProperty] public double saturation { get; set; } = 1;
    /// <summary>Gets or sets the additive brightness adjustment.</summary>
    [SerializableProperty] public double brightness { get; set; }
    /// <summary>Gets or sets contrast around middle gray.</summary>
    [SerializableProperty] public double contrast { get; set; } = 1;
    /// <summary>Gets or sets the pixelation cell width in design pixels.</summary>
    [SerializableProperty] public double pixelSize { get; set; } = 8;
    /// <summary>Gets or sets flash or letterbox color packed as RRGGBBAA.</summary>
    [SerializableProperty] public uint color { get; set; } = 0xFFFFFFFF;
    /// <summary>Gets or sets the height fraction of each cinematic bar.</summary>
    [SerializableProperty] public double letterboxRatio { get; set; } = 0.12;
    /// <summary>Gets or sets opposing color offsets in design pixels.</summary>
    [SerializableProperty] public double aberrationPixels { get; set; } = 2;
    /// <summary>Copies all mutable declaration values independently of their source asset.</summary>
    /// <returns>A detached declaration.</returns>
    public ScreenEffectDefinition Copy() => new()
    {
        kind = kind, blurRadius = blurRadius, vignetteRadius = vignetteRadius, vignetteSoftness = vignetteSoftness,
        saturation = saturation, brightness = brightness, contrast = contrast, pixelSize = pixelSize, color = color,
        letterboxRatio = letterboxRatio, aberrationPixels = aberrationPixels
    };
}

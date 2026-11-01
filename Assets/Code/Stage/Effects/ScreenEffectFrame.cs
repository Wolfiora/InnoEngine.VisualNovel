using Inno.VisualNovel.Model.Performance;

namespace Inno.VisualNovel.Stage.Effects;

/// <summary>Contains immutable evaluated post-processing parameters without source assets or GPU handles.</summary>
/// <param name="id">The stable owner-local effect identity.</param>
/// <param name="kind">The image operation.</param>
/// <param name="strength">Normalized evaluated blend strength.</param>
/// <param name="includeUi">Whether to apply after game UI composition.</param>
/// <param name="order">Explicit stack order.</param>
/// <param name="blurRadius">Blur radius in design pixels.</param>
/// <param name="vignetteRadius">Normalized inner vignette radius.</param>
/// <param name="vignetteSoftness">Normalized vignette edge.</param>
/// <param name="saturation">Color saturation multiplier.</param>
/// <param name="brightness">Additive color brightness.</param>
/// <param name="contrast">Contrast around middle gray.</param>
/// <param name="pixelSize">Pixelation cell width.</param>
/// <param name="color">Packed flash or bar color.</param>
/// <param name="letterboxRatio">Height fraction of each cinematic bar.</param>
/// <param name="aberrationPixels">Opposing color offset.</param>
public sealed record ScreenEffectFrame(string id, ScreenEffectKind kind, double strength, bool includeUi, int order,
    double blurRadius, double vignetteRadius, double vignetteSoftness, double saturation, double brightness,
    double contrast, double pixelSize, uint color, double letterboxRatio, double aberrationPixels);

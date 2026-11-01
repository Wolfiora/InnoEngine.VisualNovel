namespace Inno.VisualNovel.Stage.Effects;

/// <summary>Describes one live, deterministic particle without device resources.</summary>
/// <param name="index">Stable birth index within the emitter.</param>
/// <param name="x">Horizontal center.</param>
/// <param name="y">Vertical center.</param>
/// <param name="width">Sprite width.</param>
/// <param name="height">Sprite height.</param>
/// <param name="rotation">Rotation in degrees.</param>
/// <param name="color">Packed RRGGBBAA tint.</param>
public sealed record ParticleSample(long index, double x, double y, double width, double height, double rotation, uint color);

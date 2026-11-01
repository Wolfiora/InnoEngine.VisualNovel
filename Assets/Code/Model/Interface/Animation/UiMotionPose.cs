using System;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Interface.Animation;

/// <summary>Declares relative presentation channels without changing layout or text wrapping.</summary>
[GenerateSerializationConverter]
public sealed class UiMotionPose : ISerializable
{
    /// <summary>Gets or sets opacity multiplied with the authored style and ancestor presentation.</summary>
    [SerializableProperty] public float opacity { get; set; } = 1;
    /// <summary>Gets or sets horizontal translation in design pixels.</summary>
    [SerializableProperty] public float x { get; set; }
    /// <summary>Gets or sets vertical translation in design pixels.</summary>
    [SerializableProperty] public float y { get; set; }
    /// <summary>Gets or sets uniform scale around the authored layout pivot.</summary>
    [SerializableProperty] public float scale { get; set; } = 1;

    /// <summary>Rejects unsafe or nonfinite presentation values before a page is instantiated.</summary>
    public void Validate()
    {
        if (!float.IsFinite(opacity) || opacity is < 0 or > 1 || !float.IsFinite(x) || Math.Abs(x) > 10000
            || !float.IsFinite(y) || Math.Abs(y) > 10000 || !float.IsFinite(scale) || scale is < .1f or > 4)
            throw new InvalidOperationException("UI motion requires opacity 0–1, translation within 10000 pixels and scale 0.1–4.");
    }
    /// <summary>Creates a detached declaration for another document or session.</summary>
    /// <returns>An independently editable pose.</returns>
    public UiMotionPose Copy() => new() { opacity = opacity, x = x, y = y, scale = scale };
}

using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Stages;

/// <summary>Declares a two-dimensional local transform in design pixels.</summary>
[GenerateSerializationConverter]
public sealed class StageTransform : ISerializable
{
    /// <summary>Creates an independent declaration with the same transform components.</summary>
    /// <returns>A detached transform value.</returns>
    public StageTransform Copy() => new() { x = x, y = y, pivotX = pivotX, pivotY = pivotY, scaleX = scaleX, scaleY = scaleY, rotation = rotation, opacity = opacity };
    /// <summary>Gets or sets horizontal translation.</summary>
    [SerializableProperty] public double x { get; set; }
    /// <summary>Gets or sets vertical translation.</summary>
    [SerializableProperty] public double y { get; set; }
    /// <summary>Gets or sets the local horizontal pivot mapped to the placement position.</summary>
    [SerializableProperty] public double pivotX { get; set; }
    /// <summary>Gets or sets the local vertical pivot mapped to the placement position.</summary>
    [SerializableProperty] public double pivotY { get; set; }
    /// <summary>Gets or sets horizontal scale; negative values mirror the image.</summary>
    [SerializableProperty] public double scaleX { get; set; } = 1;
    /// <summary>Gets or sets vertical scale.</summary>
    [SerializableProperty] public double scaleY { get; set; } = 1;
    /// <summary>Gets or sets clockwise rotation in degrees.</summary>
    [SerializableProperty] public double rotation { get; set; }
    /// <summary>Gets or sets alpha multiplier.</summary>
    [SerializableProperty] public double opacity { get; set; } = 1;
}

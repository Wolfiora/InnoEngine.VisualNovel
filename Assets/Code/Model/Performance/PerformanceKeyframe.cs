using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Performance;

/// <summary>Stores one scalar value at a nonnegative logical time.</summary>
[GenerateSerializationConverter]
public sealed class PerformanceKeyframe : ISerializable
{
    /// <summary>Gets or sets the time in seconds.</summary>
    [SerializableProperty]
    public double time { get; set; }
    /// <summary>Gets or sets the finite scalar value.</summary>
    [SerializableProperty]
    public double value { get; set; }
    /// <summary>Gets or sets interpolation toward the next keyframe.</summary>
    [SerializableProperty]
    public PerformanceInterpolation interpolation { get; set; }
    /// <summary>Gets or sets normalized handles when interpolation uses CubicBezier.</summary>
    [SerializableProperty] public PerformanceCurve curve { get; set; } = new();
}

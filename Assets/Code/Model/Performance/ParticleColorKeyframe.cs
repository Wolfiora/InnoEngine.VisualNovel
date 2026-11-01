using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Performance;

/// <summary>Declares a color and its outgoing timing curve at normalized particle age.</summary>
[GenerateSerializationConverter]
public sealed class ParticleColorKeyframe : ISerializable
{
    /// <summary>Gets or sets normalized lifetime progress between zero and one.</summary>
    [SerializableProperty] public double progress { get; set; }
    /// <summary>Gets or sets the packed RRGGBBAA tint at this point.</summary>
    [SerializableProperty] public uint color { get; set; } = 0xFFFFFFFF;
    /// <summary>Gets or sets interpolation toward the next color stop.</summary>
    [SerializableProperty] public PerformanceInterpolation interpolation { get; set; }
    /// <summary>Gets or sets normalized handles when cubic timing is selected.</summary>
    [SerializableProperty] public PerformanceCurve curve { get; set; } = new();
    /// <summary>Creates an independently editable color stop.</summary>
    /// <returns>A detached value retaining its timing handles.</returns>
    public ParticleColorKeyframe Copy() => new() { progress = progress, color = color, interpolation = interpolation, curve = curve.Copy() };
}

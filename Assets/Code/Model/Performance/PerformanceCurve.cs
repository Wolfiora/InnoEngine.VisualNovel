using System;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Performance;

/// <summary>Declares a bounded cubic timing curve from (0,0) to (1,1).</summary>
[GenerateSerializationConverter]
public sealed class PerformanceCurve : ISerializable
{
    /// <summary>Gets or sets the first handle's normalized time.</summary>
    [SerializableProperty] public double x1 { get; set; } = .25;
    /// <summary>Gets or sets the first handle's normalized progress.</summary>
    [SerializableProperty] public double y1 { get; set; } = .1;
    /// <summary>Gets or sets the second handle's normalized time.</summary>
    [SerializableProperty] public double x2 { get; set; } = .25;
    /// <summary>Gets or sets the second handle's normalized progress.</summary>
    [SerializableProperty] public double y2 { get; set; } = 1;
    /// <summary>Validates finite handles within the normalized timing square.</summary>
    public void Validate()
    {
        if (!Unit(x1) || !Unit(y1) || !Unit(x2) || !Unit(y2)) throw new InvalidOperationException("Timing-curve handles must be finite values between zero and one.");
    }
    /// <summary>Creates an independently editable curve declaration.</summary>
    /// <returns>A detached handle set.</returns>
    public PerformanceCurve Copy() => new() { x1 = x1, y1 = y1, x2 = x2, y2 = y2 };
    private static bool Unit(double value) => double.IsFinite(value) && value >= 0 && value <= 1;
}

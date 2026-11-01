using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Performance;

/// <summary>Declares image-transition parameters evaluated from a restorable logical clock.</summary>
[GenerateSerializationConverter]
public sealed class TransitionDefinition : ISerializable
{
    /// <summary>Gets or sets the composition mechanism.</summary>
    [SerializableProperty] public TransitionKind kind { get; set; } = TransitionKind.Dissolve;
    /// <summary>Gets or sets the positive duration in logical seconds.</summary>
    [SerializableProperty] public double duration { get; set; } = 0.6;
    /// <summary>Gets or sets directional reveal or motion in degrees.</summary>
    [SerializableProperty] public double angle { get; set; }
    /// <summary>Gets or sets normalized reveal-edge softness.</summary>
    [SerializableProperty] public double softness { get; set; } = 0.04;
    /// <summary>Gets or sets an intermediate fade color packed as RRGGBBAA.</summary>
    [SerializableProperty] public uint color { get; set; } = 0x000000FF;
    /// <summary>Gets or sets the normalized aperture center.</summary>
    [SerializableProperty] public double centerX { get; set; } = 0.5;
    /// <summary>Gets or sets the normalized aperture center.</summary>
    [SerializableProperty] public double centerY { get; set; } = 0.5;
    /// <summary>Gets or sets whether an aperture reveals from the outside inward.</summary>
    [SerializableProperty] public bool reverse { get; set; }
    /// <summary>Gets or sets the procedural mask pattern.</summary>
    [SerializableProperty] public TransitionMask mask { get; set; }
    /// <summary>Gets or sets the deterministic mask seed.</summary>
    [SerializableProperty] public uint seed { get; set; } = 1;
    /// <summary>Copies all mutable declaration values independently of their source asset.</summary>
    /// <returns>A detached transition declaration.</returns>
    public TransitionDefinition Copy() => new()
    {
        kind = kind, duration = duration, angle = angle, softness = softness, color = color,
        centerX = centerX, centerY = centerY, reverse = reverse, mask = mask, seed = seed
    };
}

/// <summary>Identifies procedural masks requiring no undeclared texture resources.</summary>
public enum TransitionMask
{
    /// <summary>A seeded cell-wise dissolve.</summary>
    Noise,
    /// <summary>Alternating square reveals.</summary>
    Checker,
    /// <summary>Repeated horizontal blind openings.</summary>
    Blinds,
    /// <summary>A center-out diamond reveal.</summary>
    Diamond
}

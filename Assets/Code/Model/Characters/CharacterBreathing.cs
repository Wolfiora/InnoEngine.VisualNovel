using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Characters;

/// <summary>Declares subtle optional vertical breathing around the character's authored lower extent.</summary>
[GenerateSerializationConverter]
public sealed class CharacterBreathing : ISerializable
{
    /// <summary>Gets or sets whether breathing contributes an independent deformation channel.</summary>
    [SerializableProperty] public bool enabled { get; set; }
    /// <summary>Gets or sets relative scale amplitude, from zero to 0.1.</summary>
    [SerializableProperty] public double amplitude { get; set; } = 0.008;
    /// <summary>Gets or sets one complete breathing period in logical seconds.</summary>
    [SerializableProperty] public double period { get; set; } = 4;
    /// <summary>Gets or sets the normalized horizontal anchor within the pose extent.</summary>
    [SerializableProperty] public double anchorX { get; set; } = 0.5;
    /// <summary>Gets or sets the normalized vertical anchor within the pose extent.</summary>
    [SerializableProperty] public double anchorY { get; set; } = 1;
    /// <summary>Copies the breathing declaration independently of its source asset.</summary>
    /// <returns>An editable copy.</returns>
    public CharacterBreathing Copy() => new() { enabled = enabled, amplitude = amplitude, period = period, anchorX = anchorX, anchorY = anchorY };
}

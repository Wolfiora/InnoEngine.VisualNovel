using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Characters;

/// <summary>Overrides one existing part without destructively editing a pose.</summary>
[GenerateSerializationConverter]
public sealed class CharacterPartOverride : ISerializable
{
    /// <summary>Gets or sets the target part identity.</summary>
    [SerializableProperty] public string partId { get; set; } = string.Empty;
    /// <summary>Gets or sets a replacement texture index, or -1 to preserve the pose image.</summary>
    [SerializableProperty] public int texture { get; set; } = -1;
    /// <summary>Gets or sets visibility: -1 preserves, 0 hides, 1 shows.</summary>
    [SerializableProperty] public int visibility { get; set; } = -1;
    /// <summary>Gets or sets an additive horizontal adjustment.</summary>
    [SerializableProperty] public double offsetX { get; set; }
    /// <summary>Gets or sets an additive vertical adjustment.</summary>
    [SerializableProperty] public double offsetY { get; set; }
}

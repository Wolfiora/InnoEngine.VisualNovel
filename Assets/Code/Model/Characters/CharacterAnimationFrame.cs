using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Characters;

/// <summary>Declares one image and its exact logical hold duration.</summary>
[GenerateSerializationConverter]
public sealed class CharacterAnimationFrame : ISerializable
{
    /// <summary>Gets or sets the character-local texture index.</summary>
    [SerializableProperty] public int texture { get; set; }
    /// <summary>Gets or sets a positive logical duration in seconds.</summary>
    [SerializableProperty] public double seconds { get; set; } = 0.1;
}

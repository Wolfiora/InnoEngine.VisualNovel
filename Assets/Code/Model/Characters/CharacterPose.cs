using System.Linq;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Characters;

/// <summary>Declares one named body pose and its layered parts.</summary>
[GenerateSerializationConverter]
public sealed class CharacterPose : ISerializable
{
    /// <summary>Gets or sets the stable pose identity.</summary>
    [SerializableProperty] public string id { get; set; } = "default";
    /// <summary>Gets or sets the display name.</summary>
    [SerializableProperty] public string name { get; set; } = "默认姿势";
    /// <summary>Gets or sets authored parts.</summary>
    [SerializableProperty] public CharacterPart[] parts { get; set; } = [];

    /// <summary>Creates detached pose parts while preserving their shared semantic identities.</summary>
    /// <returns>An independent pose with the same identity.</returns>
    public CharacterPose Copy() => new() { id = id, name = name, parts = parts.Select(static part => part.Copy()).ToArray() };
}

using System.Linq;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Characters;

/// <summary>Declares one named, nondestructive expression combination.</summary>
[GenerateSerializationConverter]
public sealed class CharacterExpression : ISerializable
{
    /// <summary>Gets or sets the stable expression identity.</summary>
    [SerializableProperty] public string id { get; set; } = "neutral";
    /// <summary>Gets or sets the display name.</summary>
    [SerializableProperty] public string name { get; set; } = "平静";
    /// <summary>Gets or sets explicit part overrides.</summary>
    [SerializableProperty] public CharacterPartOverride[] parts { get; set; } = [];

    /// <summary>Creates detached overrides while preserving semantic part identities.</summary>
    /// <returns>An independent expression declaration with the same identity.</returns>
    public CharacterExpression Copy() => new()
    {
        id = id, name = name, parts = parts.Select(static part => new CharacterPartOverride
        { partId = part.partId, texture = part.texture, visibility = part.visibility, offsetX = part.offsetX, offsetY = part.offsetY }).ToArray()
    };
}

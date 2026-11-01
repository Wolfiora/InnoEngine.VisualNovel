using InnoEngine.Rendering;
using InnoEngine.Assets;
using InnoEngine.Serialization;
using System;

namespace Inno.VisualNovel.Model.Localization;

/// <summary>Replaces one explicit imported image in a language without changing authored stage or UI identity.</summary>
[GenerateSerializationConverter]
public sealed class LocalizedImageVariant : ISerializable
{
    /// <summary>Gets or sets the original imported resource.</summary>
    [SerializableProperty] public TextureAsset? source { get; set; }
    /// <summary>Gets or sets the language-specific imported replacement.</summary>
    [SerializableProperty] public TextureAsset? image { get; set; }
    /// <summary>Copies the mapping while retaining canonical borrowed resource identities.</summary>
    /// <returns>An independent mapping declaration.</returns>
    public LocalizedImageVariant Copy() => new() { source = source, image = image };
    /// <summary>Matches the engine's persistent asset identity, retaining reference identity for detached in-memory resources.</summary>
    /// <param name="candidate">The selected original resource.</param>
    /// <returns>Whether this mapping addresses that resource.</returns>
    public bool Matches(TextureAsset candidate) => ReferenceEquals(source, candidate)
        || source is not null && candidate.identity.persistentId != Guid.Empty && source.identity.persistentId == candidate.identity.persistentId;
}

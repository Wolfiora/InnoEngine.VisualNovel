using InnoEngine.Rendering;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Collections;

/// <summary>Declares one stable CG variant and its optional progress gate without storing runtime viewer state.</summary>
[GenerateSerializationConverter]
public sealed class GalleryVariant : ISerializable
{
    /// <summary>Gets or sets the entry-local stable variant identity.</summary>
    [SerializableProperty] public string id { get; set; } = string.Empty;
    /// <summary>Gets or sets the localized-title source.</summary>
    [SerializableProperty] public string title { get; set; } = string.Empty;
    /// <summary>Gets or sets the imported full-resolution image.</summary>
    [SerializableProperty] public TextureAsset? image { get; set; }
    /// <summary>Gets or sets an additional declared collection unlock identity; empty requires only the owning CG entry.</summary>
    [SerializableProperty] public string unlockEntryId { get; set; } = string.Empty;
    /// <summary>Copies metadata while borrowing the canonical imported image identity.</summary>
    /// <returns>An independently editable variant.</returns>
    public GalleryVariant Copy() => new() { id = id, title = title, image = image, unlockEntryId = unlockEntryId };
}

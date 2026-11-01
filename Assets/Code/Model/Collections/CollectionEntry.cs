using InnoEngine.Rendering;
using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Model.Collections;

/// <summary>Declares an unlockable collection item and its neutral presentation metadata.</summary>
[RequiresSerializationConverter]
public sealed class CollectionEntry : ISerializable
{
    /// <summary>Gets or sets the stable unlock identity.</summary>
    [SerializableProperty] public string id { get; set; } = string.Empty;
    /// <summary>Gets or sets the collection category.</summary>
    [SerializableProperty] public CollectionKind kind { get; set; }
    /// <summary>Gets or sets the source title.</summary>
    [SerializableProperty] public string title { get; set; } = string.Empty;
    /// <summary>Gets or sets article text, synopsis or credits.</summary>
    [SerializableProperty] public string description { get; set; } = string.Empty;
    /// <summary>Gets or sets the source author or composer credit.</summary>
    [SerializableProperty] public string author { get; set; } = string.Empty;
    /// <summary>Gets or sets optional artwork or a cover.</summary>
    [SerializableProperty] public TextureAsset? image { get; set; }
    /// <summary>Gets or sets ordered full-resolution CG variants; the ordinary image remains the list cover.</summary>
    [SerializableProperty] public GalleryVariant[] variants { get; set; } = [];
    /// <summary>Gets or sets the declared music cue.</summary>
    [SerializableProperty] public string musicCueId { get; set; } = string.Empty;
    /// <summary>Gets or sets a replay's authored chapter identity.</summary>
    [SerializableProperty] public string chapterId { get; set; } = string.Empty;
    /// <summary>Gets or sets a replay's public entry.</summary>
    [SerializableProperty] public string entryId { get; set; } = "start";
    /// <summary>Gets or sets replay-specific non-global initial values applied before execution.</summary>
    [SerializableProperty] public CollectionVariable[] initialValues { get; set; } = [];
    /// <summary>Gets or sets whether the item is initially visible and available.</summary>
    [SerializableProperty] public bool initiallyUnlocked { get; set; }
    /// <summary>Gets or sets a pure Boolean condition that can monotonically unlock this item.</summary>
    [SerializableProperty] public ExpressionDocument unlockCondition { get; set; } = new() { literal = NovelValue.Boolean(false) };
}

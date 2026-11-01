using System.Linq;
using InnoEngine.Assets;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Authoring.AssetOrganization;

/// <summary>Stores creator-authored organization notes against an ordinary native asset reference.</summary>
[GenerateSerializationConverter]
public sealed class ResourceLabelEntry : ISerializable
{
    /// <summary>Gets or sets the canonical resource reference, including an identified Missing resource.</summary>
    [SerializableProperty] public AssetObject? asset { get; set; }
    /// <summary>Gets or sets the optional creator-defined category.</summary>
    [SerializableProperty] public string group { get; set; } = string.Empty;
    /// <summary>Gets or sets up to 32 distinct creator-defined labels.</summary>
    [SerializableProperty] public string[] tags { get; set; } = [];
    /// <summary>Gets or sets independent authoring notes, excluded from narrative state.</summary>
    [SerializableProperty] public string description { get; set; } = string.Empty;
    /// <summary>Copies annotations while borrowing the same native resource identity.</summary>
    /// <returns>An independently editable record with a detached label array.</returns>
    public ResourceLabelEntry Copy() => new() { asset = asset, group = group, tags = tags.ToArray(), description = description };
}

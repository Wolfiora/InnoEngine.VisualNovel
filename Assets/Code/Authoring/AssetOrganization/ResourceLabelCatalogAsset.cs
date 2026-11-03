using InnoEngine.Assets;
using InnoEngine.Reflection;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Authoring.AssetOrganization;

/// <summary>Owns explicit resource annotations through engine serialization; discovery and loading remain native asset services.</summary>
[StableTypeId("97039a82-1ce5-4875-b74a-278e23475331")]
public sealed class ResourceLabelCatalogAsset : AssetObject
{
    /// <summary>Gets or sets the creator-facing title of this editor-owned annotation document.</summary>
    [SerializableProperty] public string title { get; set; } = "Resource annotations";
    /// <summary>Gets or sets annotations for explicitly chosen native resources, with one record per persistent identity.</summary>
    [SerializableProperty] public ResourceLabelEntry[] entries { get; set; } = [];
}

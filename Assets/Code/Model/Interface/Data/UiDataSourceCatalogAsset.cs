using InnoEngine.Assets;
using InnoEngine.Reflection;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Interface.Data;

/// <summary>Stores neutral visual binding declarations independently of source implementation factories.</summary>
[StableTypeId("fd0c7316-14a5-4d89-8492-bfd0a0b9f1b1")]
public sealed class UiDataSourceCatalogAsset : AssetObject
{
    /// <summary>Gets or sets the catalog title.</summary>
    [SerializableProperty] public string title { get; set; } = "UI data sources";
    /// <summary>Gets or sets complete typed source schemas.</summary>
    [SerializableProperty] public UiDataSourceDefinition[] definitions { get; set; } = [];
}

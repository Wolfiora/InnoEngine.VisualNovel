using InnoEngine.Assets;
using InnoEngine.Reflection;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Extensions;

/// <summary>Distributes neutral command forms independently of implementation assemblies or runtime discovery.</summary>
[StableTypeId("fd0c7316-14a5-4d89-8492-bfd0a0b9f182")]
public sealed class NovelCommandCatalogAsset : AssetObject
{
    /// <summary>Gets or sets the author-facing catalog title.</summary>
    [SerializableProperty] public string title { get; set; } = "Custom commands";
    /// <summary>Gets or sets implemented command contracts published by their extension author.</summary>
    [SerializableProperty] public NovelCommandDefinition[] definitions { get; set; } = [];
}

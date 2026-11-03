using InnoEngine.Assets;
using InnoEngine.Reflection;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Authoring.AssetAuditing.Licenses;

/// <summary>Stores editable authoring provenance independently of runtime game state and packaging roots.</summary>
[StableTypeId("fd0c7316-14a5-4d89-8492-bfd0a0b9f1b2")]
public sealed class ResourceLicenseCatalogAsset : AssetObject
{
    /// <summary>Gets or sets the author-facing catalog title.</summary>
    [SerializableProperty] public string title { get; set; } = "Resource license records";
    /// <summary>Gets or sets unique native asset provenance records, including work-in-progress metadata.</summary>
    [SerializableProperty] public ResourceLicenseEntry[] entries { get; set; } = [];
}

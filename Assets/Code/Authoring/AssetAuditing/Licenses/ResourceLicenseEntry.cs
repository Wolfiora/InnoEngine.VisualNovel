using InnoEngine.Assets;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Authoring.AssetAuditing.Licenses;

/// <summary>Records author-supplied distribution provenance against a native asset reference.</summary>
[GenerateSerializationConverter]
public sealed class ResourceLicenseEntry : ISerializable
{
    /// <summary>Gets or sets the persistent native resource, preserving Missing references.</summary>
    [SerializableProperty] public AssetObject? asset { get; set; }
    /// <summary>Gets or sets the recorded creator or copyright holder.</summary>
    [SerializableProperty] public string author { get; set; } = string.Empty;
    /// <summary>Gets or sets a source URL or original-creation description.</summary>
    [SerializableProperty] public string source { get; set; } = string.Empty;
    /// <summary>Gets or sets the stated license name or identifier.</summary>
    [SerializableProperty] public string license { get; set; } = string.Empty;
    /// <summary>Gets or sets whether the author has confirmed redistribution for the intended game.</summary>
    [SerializableProperty] public bool redistributionConfirmed { get; set; }
    /// <summary>Gets or sets whether the stated terms require attribution.</summary>
    [SerializableProperty] public bool attributionRequired { get; set; }
    /// <summary>Gets or sets the attribution or full notice supplied by the author.</summary>
    [SerializableProperty] public string notice { get; set; } = string.Empty;
    /// <summary>Gets or sets the recorded modifications.</summary>
    [SerializableProperty] public string modifications { get; set; } = string.Empty;
    /// <summary>Copies neutral provenance fields while borrowing the canonical native resource.</summary>
    /// <returns>An independently editable entry.</returns>
    public ResourceLicenseEntry Copy() => new() { asset = asset, author = author, source = source, license = license,
        redistributionConfirmed = redistributionConfirmed, attributionRequired = attributionRequired, notice = notice, modifications = modifications };
}

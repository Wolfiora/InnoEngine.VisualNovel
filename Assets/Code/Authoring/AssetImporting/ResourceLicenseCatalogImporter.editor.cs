using System.Collections.Generic;
using InnoEditor.Assets;
using Inno.VisualNovel.Authoring.AssetAuditing.Licenses;

namespace Inno.VisualNovel.Authoring.AssetImporting;

/// <summary>Imports editor-owned provenance metadata through native serialization and asset dependencies.</summary>
[AssetImporter("inno.vn.resource-licenses")]
public sealed class ResourceLicenseCatalogImporter : NativeNovelAssetImporter<ResourceLicenseCatalogAsset>
{
    /// <inheritdoc />
    public override IReadOnlyList<string> supportedExtensions { get; } = [".ivnlicenses"];
    /// <inheritdoc />
    protected override void Validate(ResourceLicenseCatalogAsset asset) => ResourceLicenseEditService.Validate(asset);
}

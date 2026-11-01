using System.Collections.Generic;
using InnoEditor.Assets;
using Inno.VisualNovel.Authoring.AssetOrganization;

namespace Inno.VisualNovel.Authoring.AssetImporting;

/// <summary>Imports editor-owned resource annotations through native serialization and reference dependencies.</summary>
[AssetImporter("inno.vn.resource-labels")]
public sealed class ResourceLabelCatalogImporter : NativeNovelAssetImporter<ResourceLabelCatalogAsset>
{
    /// <inheritdoc />
    public override IReadOnlyList<string> supportedExtensions { get; } = [".ivnlabels"];
    /// <inheritdoc />
    protected override void Validate(ResourceLabelCatalogAsset asset) => ResourceLabelEditService.Validate(asset);
}

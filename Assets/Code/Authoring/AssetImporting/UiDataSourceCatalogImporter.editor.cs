using System.Collections.Generic;
using InnoEditor.Assets;
using Inno.VisualNovel.Model.Interface.Data;

namespace Inno.VisualNovel.Authoring.AssetImporting;

/// <summary>Imports typed custom UI data contracts through the native asset pipeline.</summary>
[AssetImporter("inno.vn.ui-data")]
public sealed class UiDataSourceCatalogImporter : NativeNovelAssetImporter<UiDataSourceCatalogAsset>
{
    /// <inheritdoc />
    public override IReadOnlyList<string> supportedExtensions { get; } = [".ivnuidata"];
    /// <inheritdoc />
    protected override void Validate(UiDataSourceCatalogAsset asset) => UiDataSourceContract.ValidateCatalog(asset.definitions);
}

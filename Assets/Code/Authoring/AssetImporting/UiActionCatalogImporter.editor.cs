using System.Collections.Generic;
using InnoEditor.Assets;
using Inno.VisualNovel.Model.Interface.Actions;

namespace Inno.VisualNovel.Authoring.AssetImporting;

/// <summary>Imports neutral UI action contracts through the engine's native asset pipeline.</summary>
[AssetImporter("inno.vn.ui-actions")]
public sealed class UiActionCatalogImporter : NativeNovelAssetImporter<UiActionCatalogAsset>
{
    /// <inheritdoc />
    public override IReadOnlyList<string> supportedExtensions { get; } = [".ivnuiactions"];
    /// <inheritdoc />
    protected override void Validate(UiActionCatalogAsset asset) => UiActionContract.ValidateCatalog(asset.definitions);
}

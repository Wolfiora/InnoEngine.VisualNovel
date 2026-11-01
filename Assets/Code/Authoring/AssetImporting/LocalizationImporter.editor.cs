using System.Collections.Generic;
using InnoEditor.Assets;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Player.Localization;

namespace Inno.VisualNovel.Authoring.AssetImporting;

/// <summary>Validates language identities, translation revisions and interpolation before publishing a native table.</summary>
[AssetImporter("inno.vn.localization")]
public sealed class LocalizationImporter : NativeNovelAssetImporter<LocalizationTableAsset>
{
    /// <inheritdoc />
    public override IReadOnlyList<string> supportedExtensions { get; } = [".ivnlocale"];
    /// <inheritdoc />
    protected override void Validate(LocalizationTableAsset asset) => _ = new NovelLocalizationService(asset, asset.sourceLanguage);
}

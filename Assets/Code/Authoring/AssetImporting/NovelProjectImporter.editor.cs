using System.Collections.Generic;
using InnoEditor.Assets;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Authoring.Publishing;

namespace Inno.VisualNovel.Authoring.AssetImporting;

/// <summary>Validates native game definitions and their explicit content roots.</summary>
[AssetImporter("inno.vn.project")]
public sealed class NovelProjectImporter : NativeNovelAssetImporter<NovelProjectAsset>
{
    /// <inheritdoc />
    public override IReadOnlyList<string> supportedExtensions { get; } = [".ivnproject"];
    /// <inheritdoc />
    protected override NovelProjectAsset Prepare(
        AssetImportContext context,
        NovelProjectAsset asset
    ) {
        Validate(asset);
        asset.buildFingerprint = ProjectContentFingerprint.Compute(context, asset);
        return asset;
    }
    /// <inheritdoc />
    protected override void Validate(NovelProjectAsset asset)
    {
        NovelProjectValidator.Validate(asset);
    }
}

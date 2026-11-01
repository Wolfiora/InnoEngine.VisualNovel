using System.Collections.Generic;
using InnoEditor.Assets;
using Inno.VisualNovel.Authoring.Preview;

namespace Inno.VisualNovel.Authoring.AssetImporting;

/// <summary>Imports editor-only native preview cases without entering the runtime content closure.</summary>
[AssetImporter("inno.vn.preview-case")]
public sealed class PreviewCaseImporter : NativeNovelAssetImporter<NovelPreviewCaseAsset>
{
    /// <inheritdoc />
    public override IReadOnlyList<string> supportedExtensions { get; } = [".ivntest"];
    /// <inheritdoc />
    protected override void Validate(NovelPreviewCaseAsset asset) => PreviewCaseRules.Validate(asset);
}

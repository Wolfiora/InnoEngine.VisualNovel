using System.Collections.Generic;
using InnoEditor.Assets;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Authoring.AssetImporting;

/// <summary>Validates native UI trees before making them available to a game.</summary>
[AssetImporter("inno.vn.screen")]
public sealed class ScreenImporter : NativeNovelAssetImporter<ScreenAsset>
{
    /// <inheritdoc />
    public override IReadOnlyList<string> supportedExtensions { get; } = [".ivnui"];
    /// <inheritdoc />
    protected override void Validate(ScreenAsset asset)
    {
        _ = new UiDocumentInstance(asset, static (element, _) => (0, element.style.fontSize));
        Inno.VisualNovel.Authoring.UiEditing.ScreenTextValidator.Validate(asset);
    }
}

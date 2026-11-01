using System.Collections.Generic;
using InnoEditor.Assets;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.UI.Documents;
using Inno.VisualNovel.UI.Theming;

namespace Inno.VisualNovel.Authoring.AssetImporting;

/// <summary>Validates native token, inheritance and page overrides before publishing a theme.</summary>
[AssetImporter("inno.vn.theme")]
public sealed class ThemeImporter : NativeNovelAssetImporter<ThemeAsset>
{
    /// <inheritdoc />
    public override IReadOnlyList<string> supportedExtensions { get; } = [".ivntheme"];
    /// <inheritdoc />
    protected override void Validate(ThemeAsset asset)
    {
        _ = new UiThemeResolver(asset);
        foreach (var binding in asset.screens)
            _ = new UiDocumentInstance(binding.screen!, static (element, _) => (0, element.style.fontSize), theme: asset);
    }
}

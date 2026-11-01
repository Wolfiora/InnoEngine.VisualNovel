using System;
using InnoEngine.Assets;
using InnoEditor.Assets;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Model.Collections;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Authoring.Documents;

internal static class NativeResourceSources
{
    internal static bool Accepts(AssetPath path) => Kind(path) is ".ivnvariables" or ".ivnui" or ".ivntheme" or ".ivnstage" or ".ivncharacter" or ".ivnclip" or ".ivnaudio" or ".ivnproject" or ".ivneffect" or ".ivnlocale" or ".ivncollection" or ".ivncommands" or ".ivnuiactions" or ".ivnuidata" or ".ivnlicenses" or ".ivnlabels" or ".ivntest";
    internal static AssetObject Decode(
        AssetPath path,
        byte[] bytes
    ) => Kind(path) switch
    {
        ".ivnvariables" => EditorAssets.DecodeNative<VariableTableAsset>(bytes),
        ".ivnui" => EditorAssets.DecodeNative<ScreenAsset>(bytes),
        ".ivntheme" => EditorAssets.DecodeNative<ThemeAsset>(bytes),
        ".ivnstage" => EditorAssets.DecodeNative<StageAsset>(bytes),
        ".ivncharacter" => EditorAssets.DecodeNative<CharacterAsset>(bytes),
        ".ivnclip" => EditorAssets.DecodeNative<PerformanceClipAsset>(bytes),
        ".ivnaudio" => EditorAssets.DecodeNative<AudioCueAsset>(bytes),
        ".ivnproject" => EditorAssets.DecodeNative<NovelProjectAsset>(bytes),
        ".ivneffect" => EditorAssets.DecodeNative<VisualEffectAsset>(bytes),
        ".ivnlocale" => EditorAssets.DecodeNative<LocalizationTableAsset>(bytes),
        ".ivncollection" => EditorAssets.DecodeNative<CollectionAsset>(bytes),
        ".ivncommands" => EditorAssets.DecodeNative<Model.Extensions.NovelCommandCatalogAsset>(bytes),
        ".ivnuiactions" => EditorAssets.DecodeNative<Model.Interface.Actions.UiActionCatalogAsset>(bytes),
        ".ivnuidata" => EditorAssets.DecodeNative<Model.Interface.Data.UiDataSourceCatalogAsset>(bytes),
        ".ivnlicenses" => EditorAssets.DecodeNative<AssetAuditing.Licenses.ResourceLicenseCatalogAsset>(bytes),
        ".ivnlabels" => EditorAssets.DecodeNative<AssetOrganization.ResourceLabelCatalogAsset>(bytes),
        ".ivntest" => EditorAssets.DecodeNative<Preview.NovelPreviewCaseAsset>(bytes),
        _ => throw new InvalidOperationException("The native resource type is unsupported.")
    };
    internal static bool Save(
        AssetPath path,
        byte[] bytes
    ) {
        AssetObject asset = Decode(path, bytes);
        return EditorAssets.Save(path, asset);
    }
    internal static string Extension<T>() where T : AssetObject => typeof(T) == typeof(ScreenAsset) ? ".ivnui" : typeof(T) == typeof(ThemeAsset) ? ".ivntheme" : typeof(T) == typeof(StageAsset) ? ".ivnstage"
        : typeof(T) == typeof(VariableTableAsset) ? ".ivnvariables"
        : typeof(T) == typeof(CharacterAsset) ? ".ivncharacter" : typeof(T) == typeof(PerformanceClipAsset) ? ".ivnclip" : typeof(T) == typeof(AudioCueAsset) ? ".ivnaudio"
        : typeof(T) == typeof(NovelProjectAsset) ? ".ivnproject" : typeof(T) == typeof(VisualEffectAsset) ? ".ivneffect" : typeof(T) == typeof(LocalizationTableAsset) ? ".ivnlocale"
        : typeof(T) == typeof(CollectionAsset) ? ".ivncollection" : typeof(T) == typeof(Model.Extensions.NovelCommandCatalogAsset) ? ".ivncommands"
        : typeof(T) == typeof(Model.Interface.Actions.UiActionCatalogAsset) ? ".ivnuiactions"
        : typeof(T) == typeof(Model.Interface.Data.UiDataSourceCatalogAsset) ? ".ivnuidata"
        : typeof(T) == typeof(AssetAuditing.Licenses.ResourceLicenseCatalogAsset) ? ".ivnlicenses"
        : typeof(T) == typeof(AssetOrganization.ResourceLabelCatalogAsset) ? ".ivnlabels"
        : typeof(T) == typeof(Preview.NovelPreviewCaseAsset) ? ".ivntest" : throw new InvalidOperationException("This asset has no VN resource document format.");
    private static string Kind(AssetPath path) => System.IO.Path.GetExtension(path.localPath).ToLowerInvariant();
}

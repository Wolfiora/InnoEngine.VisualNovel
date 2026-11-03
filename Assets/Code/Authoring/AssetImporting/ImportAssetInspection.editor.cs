using System;
using System.Collections.Generic;
using InnoEngine.Assets;
using InnoEngine.Audio;
using InnoEngine.Rendering;
using InnoEngine.Text;
using InnoEditor.Assets;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Collections;
using Inno.VisualNovel.Model.Extensions;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Interface.Actions;
using Inno.VisualNovel.Model.Interface.Data;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Authoring.Diagnostics;

namespace Inno.VisualNovel.Authoring.AssetImporting;

internal static class ImportAssetInspection
{
    internal static IReadOnlyList<AssetDependency> SourceDependencies(
        AssetObject asset,
        ReadOnlySpan<byte> source,
        AssetSerializationServices services
    ) => asset switch
    {
        NovelProjectAsset => Read<NovelProjectAsset>(source, services), StoryAsset => Read<StoryAsset>(source, services),
        VariableTableAsset => Read<VariableTableAsset>(source, services), CharacterAsset => Read<CharacterAsset>(source, services),
        StageAsset => Read<StageAsset>(source, services), PerformanceClipAsset => Read<PerformanceClipAsset>(source, services),
        VisualEffectAsset => Read<VisualEffectAsset>(source, services), ScreenAsset => Read<ScreenAsset>(source, services),
        ThemeAsset => Read<ThemeAsset>(source, services), AudioCueAsset => Read<AudioCueAsset>(source, services),
        MaterialAsset => Read<MaterialAsset>(source, services),
        CollectionAsset => Read<CollectionAsset>(source, services), LocalizationTableAsset => Read<LocalizationTableAsset>(source, services),
        NovelCommandCatalogAsset => Read<NovelCommandCatalogAsset>(source, services),
        UiActionCatalogAsset => Read<UiActionCatalogAsset>(source, services), UiDataSourceCatalogAsset => Read<UiDataSourceCatalogAsset>(source, services),
        _ => Capture(asset, services).dependencies
    };

    private static IReadOnlyList<AssetDependency> Read<TAsset>(
        ReadOnlySpan<byte> source,
        AssetSerializationServices services
    ) where TAsset : AssetObject
    {
        _ = NativeAssetSourceSerialization.Import<TAsset>(source, services, out var dependencies);
        return dependencies;
    }

    internal static (byte[] data, IReadOnlyList<AssetDependency> dependencies) Capture(
        AssetObject asset,
        AssetSerializationServices services
    ) => asset switch
    {
        NovelProjectAsset value => Inspect(value, services), StoryAsset value => Inspect(value, services),
        VariableTableAsset value => Inspect(value, services), CharacterAsset value => Inspect(value, services), StageAsset value => Inspect(value, services),
        PerformanceClipAsset value => Inspect(value, services), VisualEffectAsset value => Inspect(value, services),
        ScreenAsset value => Inspect(value, services), ThemeAsset value => Inspect(value, services), AudioCueAsset value => Inspect(value, services),
        CollectionAsset value => Inspect(value, services), LocalizationTableAsset value => Inspect(value, services),
        NovelCommandCatalogAsset value => Inspect(value, services), UiActionCatalogAsset value => Inspect(value, services), UiDataSourceCatalogAsset value => Inspect(value, services),
        MaterialAsset value => Inspect(value, services), ShaderAsset value => Inspect(value, services),
        TextureAsset value => (NativeAssetSourceSerialization.Export(value, services), []),
        FontAsset value => (NativeAssetSourceSerialization.Export(value, services), []),
        AudioClipAsset value => (NativeAssetSourceSerialization.Export(value, services), []),
        _ => throw new AuthoringValidationException($"The importer cannot inspect this additional resource without registering dependencies twice: {asset.assetPath}. Read-only property/dependency capture is required. The original resource is preserved.")
    };
    private static (byte[] data, IReadOnlyList<AssetDependency> dependencies) Inspect<TAsset>(
        TAsset asset,
        AssetSerializationServices services
    ) where TAsset : AssetObject
    {
        // The native source codec collects references locally without the import service's dependency sink.
        // Its typed read supplies the same official dependency descriptors; no private envelope reader is used.
        byte[] data = NativeAssetSourceSerialization.Export(asset, services);
        _ = NativeAssetSourceSerialization.Import<TAsset>(data, services, out var dependencies);
        return (data, dependencies);
    }
}

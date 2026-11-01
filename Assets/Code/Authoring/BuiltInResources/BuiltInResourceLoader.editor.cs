using System;
using InnoEngine.Assets;
using InnoEngine.Rendering;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Authoring.Diagnostics;

namespace Inno.VisualNovel.Authoring.BuiltInResources;

/// <summary>Loads explicitly catalogued built-in declarations through the engine asset owner.</summary>
public static class BuiltInResourceLoader
{
    /// <summary>Resolves the catalogued native type without scanning directories or creating a second asset index.</summary>
    /// <param name="entry">One shipped preset descriptor.</param>
    /// <returns>The current canonical native asset; missing resources produce an explicit failure.</returns>
    public static AssetObject Load(BuiltInResourceEntry entry)
    {
        AssetObject asset = entry.kind switch
        {
            BuiltInResourceKind.Theme => BuiltInNovelAssets.Load<ThemeAsset>(entry.path),
            BuiltInResourceKind.Stage => BuiltInNovelAssets.Load<StageAsset>(entry.path),
            BuiltInResourceKind.Character => BuiltInNovelAssets.Load<CharacterAsset>(entry.path),
            BuiltInResourceKind.Motion => BuiltInNovelAssets.Load<PerformanceClipAsset>(entry.path),
            BuiltInResourceKind.Audio => BuiltInNovelAssets.Load<AudioCueAsset>(entry.path),
            BuiltInResourceKind.Icon => BuiltInNovelAssets.Load<TextureAsset>(entry.path),
            BuiltInResourceKind.Widget => BuiltInNovelAssets.Load<ScreenAsset>(entry.path),
            BuiltInResourceKind.Particle or BuiltInResourceKind.Transition or BuiltInResourceKind.ScreenEffect => BuiltInNovelAssets.Load<VisualEffectAsset>(entry.path),
            _ => throw new ArgumentOutOfRangeException(nameof(entry))
        };
        if (asset.isMissing) throw new AuthoringValidationException($"内置资源缺失：{entry.path}");
        return asset;
    }
}

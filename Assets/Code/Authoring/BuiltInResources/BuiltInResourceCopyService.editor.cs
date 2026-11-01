using System;
using System.Collections.Generic;
using InnoEditor.Assets;
using InnoEngine.Assets;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Authoring.BuiltInResources;

/// <summary>Instantiates independently editable native presets without writing into installation mounts.</summary>
public static class BuiltInResourceCopyService
{
    /// <summary>Creates a new native identity and resource key, rebuilding theme-to-page references when required.</summary>
    /// <param name="source">The imported read-only or project preset.</param>
    /// <param name="created">An optional synchronous observer for each committed native output, including dependent theme pages.</param>
    /// <returns>The canonical writable copy under ordinary Game/Resources assets.</returns>
    public static AssetObject Copy(
        AssetObject source,
        Action<AssetObject>? created = null
    ) {
        if (source.isMissing) throw new InvalidOperationException("A missing preset cannot be copied.");
        string key = "copy." + Guid.NewGuid().ToString("N");
        switch (source)
        {
            case StageAsset stage:
                var stageCopy = StageAssetCopy.Copy(stage); stageCopy.resourceId = key; return Save(".ivnstage", stageCopy, created);
            case CharacterAsset character:
                var actorCopy = CharacterAssetCopy.Copy(character); actorCopy.resourceId = key; return Save(".ivncharacter", actorCopy, created);
            case PerformanceClipAsset clip:
                var motionCopy = PerformanceClipCopy.Copy(clip); motionCopy.resourceId = key; return Save(".ivnclip", motionCopy, created);
            case VisualEffectAsset effect:
                var effectCopy = VisualEffectCopy.Copy(effect); effectCopy.resourceId = key; return Save(".ivneffect", effectCopy, created);
            case ScreenAsset screen: return Save(".ivnui", UiDefinitionCopy.Copy(screen), created);
            case ThemeAsset theme:
                var themeCopy = ThemeAssetCopy.Copy(theme); themeCopy.resourceId = key;
                var pages = new Dictionary<Guid, ScreenAsset>();
                foreach (var binding in themeCopy.screens)
                {
                    var original = binding.screen ?? throw new InvalidOperationException("A theme page is missing.");
                    if (original.isMissing || original.identity.persistentId == Guid.Empty) throw new InvalidOperationException("A theme page has no available native identity.");
                    if (!pages.TryGetValue(original.identity.persistentId, out var page))
                        pages.Add(original.identity.persistentId, page = Save(".ivnui", UiDefinitionCopy.Copy(original), created));
                    binding.screen = page;
                }
                return Save(".ivntheme", themeCopy, created);
            case AudioCueAsset sound: return Save(".ivnaudio", new AudioCueAsset { resourceId = key, title = sound.title, author = sound.author,
                clip = sound.clip, channel = sound.channel, volume = sound.volume, loop = sound.loop, fadeIn = sound.fadeIn, fadeOut = sound.fadeOut }, created);
            default: throw new InvalidOperationException("This binary image is used as a native immutable reference; copy its editable source artwork to change pixels.");
        }
    }
    private static T Save<T>(
        string extension,
        T source,
        Action<AssetObject>? created
    ) where T : AssetObject
    {
        var path = AssetPath.Project("Game/Resources/" + Guid.NewGuid().ToString("N") + extension);
        if (!EditorAssets.Save(path, source)) throw new InvalidOperationException("Native preset copy could not be imported: " + path);
        T imported = Assets.Load<T>(path);
        if (imported.isMissing) throw new InvalidOperationException("Copied native resource is unavailable: " + path);
        created?.Invoke(imported);
        return imported;
    }
}

using InnoEngine.Assets;
using InnoEditor.Assets;
using InnoEditor.Interactions;

namespace Inno.VisualNovel.Editor.Presentation;

internal static class NovelResourceSelection
{
    internal static T? Get<T>(EditorInteractions interactions) where T : AssetObject
    {
        if (interactions.selection.selectedTarget is T asset) return asset;
        return interactions.selection.selectedTarget is AssetFileEntry file && Assets.TryLoad<T>(file.assetPath, out T? loaded) ? loaded : null;
    }
}

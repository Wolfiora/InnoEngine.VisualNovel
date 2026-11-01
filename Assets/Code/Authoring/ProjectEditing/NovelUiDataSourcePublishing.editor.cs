using System;
using System.Linq;
using InnoEngine.Assets;
using InnoEditor.Assets;
using Inno.VisualNovel.Model.Interface.Data;
using Inno.VisualNovel.Player.Interface.Data;

namespace Inno.VisualNovel.Authoring.ProjectEditing;

/// <summary>Publishes generation-local source registry metadata for no-code UI binding.</summary>
public static class NovelUiDataSourcePublishing
{
    /// <summary>Saves the exact neutral schemas without serializing sources or their factories.</summary>
    /// <param name="registry">The explicit current-generation implementations.</param>
    /// <param name="destination">A writable project catalog path.</param>
    /// <returns>The canonical imported metadata catalog.</returns>
    public static UiDataSourceCatalogAsset Publish(
        NovelUiDataSourceRegistry registry,
        AssetPath destination
    ) {
        if (!destination.localPath.EndsWith(".ivnuidata", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("UI source catalogs use .ivnuidata.", nameof(destination));
        if (!EditorAssets.Save(destination, new UiDataSourceCatalogAsset { definitions = registry.definitions.ToArray() })) throw new InvalidOperationException("UI source metadata catalog did not import.");
        return Assets.Load<UiDataSourceCatalogAsset>(destination);
    }
}

using System;
using System.Linq;
using InnoEngine.Assets;
using InnoEditor.Assets;
using Inno.VisualNovel.Model.Interface.Actions;
using Inno.VisualNovel.Player.Interface.Actions;

namespace Inno.VisualNovel.Authoring.ProjectEditing;

/// <summary>Publishes explicit current-generation UI action metadata as editable native forms.</summary>
public static class NovelUiActionPublishing
{
    /// <summary>Saves only neutral schemas; factories and handlers never enter the asset.</summary>
    /// <param name="registry">The registered implementations to describe.</param>
    /// <param name="destination">A writable project catalog destination.</param>
    /// <returns>The canonical imported catalog.</returns>
    public static UiActionCatalogAsset Publish(
        NovelUiActionRegistry registry,
        AssetPath destination
    ) {
        if (!destination.localPath.EndsWith(".ivnuiactions", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("UI action catalogs use .ivnuiactions.", nameof(destination));
        if (!EditorAssets.Save(destination, new UiActionCatalogAsset { definitions = registry.definitions.ToArray() })) throw new InvalidOperationException("UI action metadata catalog did not import.");
        return Assets.Load<UiActionCatalogAsset>(destination);
    }
}

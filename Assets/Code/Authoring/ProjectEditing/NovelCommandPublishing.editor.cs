using System;
using System.Linq;
using InnoEngine.Assets;
using InnoEditor.Assets;
using Inno.VisualNovel.Model.Extensions;
using Inno.VisualNovel.Narrative.Extensions;

namespace Inno.VisualNovel.Authoring.ProjectEditing;

/// <summary>Publishes current-generation C# command metadata as editable native authoring forms.</summary>
public static class NovelCommandPublishing
{
    /// <summary>Saves registry schemas through the engine asset service without serializing factories or CLR types.</summary>
    /// <param name="registry">The explicit registered implementations.</param>
    /// <param name="destination">A writable project .ivncommands asset.</param>
    /// <returns>The canonical imported catalog.</returns>
    public static NovelCommandCatalogAsset Publish(
        NovelCommandRegistry registry,
        AssetPath destination
    ) {
        if (!destination.localPath.EndsWith(".ivncommands", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Command forms use the .ivncommands native format.", nameof(destination));
        if (!EditorAssets.Save(destination, new NovelCommandCatalogAsset { definitions = registry.definitions.ToArray() })) throw new InvalidOperationException("Command metadata catalog did not import.");
        return Assets.Load<NovelCommandCatalogAsset>(destination);
    }
}

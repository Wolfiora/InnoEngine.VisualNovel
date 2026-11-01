using System;
using System.Linq;
using Inno.VisualNovel.Model.Interface.Actions;

namespace Inno.VisualNovel.Authoring.UiEditing;

/// <summary>Applies validated neutral UI action metadata edits within an existing native resource transaction.</summary>
public static class UiActionCatalogEditService
{
    /// <summary>Adds or replaces one complete contract without partially changing an invalid catalog.</summary>
    /// <param name="catalog">The detached native document.</param>
    /// <param name="definition">The complete proposed action.</param>
    /// <param name="previousId">The old identity when explicitly renaming an action.</param>
    public static void Set(
        UiActionCatalogAsset catalog,
        UiActionDefinition definition,
        string previousId = ""
    ) {
        UiActionContract.Validate(definition);
        if (previousId.Length > 0 && previousId != definition.id && catalog.definitions.Any(item => item.id == definition.id)) throw new InvalidOperationException("The target UI action identity already exists.");
        var candidate = catalog.definitions.Where(item => item.id != (previousId.Length == 0 ? definition.id : previousId)).Select(UiActionContract.Copy).Append(UiActionContract.Copy(definition)).ToArray();
        UiActionContract.ValidateCatalog(candidate); catalog.definitions = candidate;
    }
    /// <summary>Removes a schema from this catalog; attached game metadata is a separate explicit snapshot.</summary>
    /// <param name="catalog">The detached catalog document.</param>
    /// <param name="id">The stable action identity.</param>
    public static void Remove(
        UiActionCatalogAsset catalog,
        string id
    ) => catalog.definitions = catalog.definitions.Where(item => item.id != id).ToArray();
}

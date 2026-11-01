using System;
using System.Linq;
using Inno.VisualNovel.Model.Interface.Data;

namespace Inno.VisualNovel.Authoring.UiEditing;

/// <summary>Applies complete validated source schema changes inside native resource document transactions.</summary>
public static class UiDataSourceCatalogEditService
{
    /// <summary>Adds or replaces a source without mutating the document on validation failure.</summary>
    /// <param name="catalog">The detached native document.</param>
    /// <param name="definition">The complete proposed source metadata.</param>
    /// <param name="previousId">The old identity when explicitly renaming.</param>
    public static void Set(
        UiDataSourceCatalogAsset catalog,
        UiDataSourceDefinition definition,
        string previousId = ""
    ) {
        UiDataSourceContract.Validate(definition);
        if (previousId.Length > 0 && previousId != definition.id && catalog.definitions.Any(item => item.id == definition.id)) throw new InvalidOperationException("The target source identity already exists.");
        var candidate = catalog.definitions.Where(item => item.id != (previousId.Length == 0 ? definition.id : previousId)).Select(UiDataSourceContract.Copy).Append(UiDataSourceContract.Copy(definition)).ToArray();
        UiDataSourceContract.ValidateCatalog(candidate); catalog.definitions = candidate;
    }
    /// <summary>Removes one source declaration; game copies require a separate explicit update.</summary>
    /// <param name="catalog">The detached catalog.</param>
    /// <param name="id">The stable source identity.</param>
    public static void Remove(
        UiDataSourceCatalogAsset catalog,
        string id
    ) => catalog.definitions = catalog.definitions.Where(item => item.id != id).ToArray();
}

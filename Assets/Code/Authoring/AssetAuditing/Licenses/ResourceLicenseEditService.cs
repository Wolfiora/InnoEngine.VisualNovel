using System;
using System.Linq;
using InnoEngine.Assets;

namespace Inno.VisualNovel.Authoring.AssetAuditing.Licenses;

/// <summary>Applies bounded native license metadata edits without interpreting or inventing legal permissions.</summary>
public static class ResourceLicenseEditService
{
    /// <summary>Checks source identity uniqueness and safe metadata bounds; incomplete records remain editable.</summary>
    /// <param name="catalog">The proposed native authoring asset.</param>
    public static void Validate(ResourceLicenseCatalogAsset catalog) => ValidateEntries(catalog.title, catalog.entries);
    private static void ValidateEntries(
        string title,
        ResourceLicenseEntry[] entries
    ) {
        if (title.Length > 256 || entries.Length > 10000) throw new InvalidOperationException("License catalogs require bounded titles and at most 10000 records.");
        foreach (var entry in entries)
            if (entry.asset is null || entry.asset.identity.persistentId == Guid.Empty || entry.asset is ResourceLicenseCatalogAsset
                || entry.author.Length > 4096 || entry.source.Length > 4096 || entry.license.Length > 1024 || entry.notice.Length > 65536 || entry.modifications.Length > 16384)
                throw new InvalidOperationException("License entries need persistent non-catalog resources and bounded provenance text.");
        if (entries.Select(static entry => entry.asset!.identity.persistentId).Distinct().Count() != entries.Length)
            throw new InvalidOperationException("A native resource can have only one current provenance entry in a catalog.");
    }
    /// <summary>Adds or replaces a complete entry atomically inside an existing native History transaction.</summary>
    /// <param name="catalog">The detached source document.</param>
    /// <param name="entry">The full proposed provenance record.</param>
    public static void Set(
        ResourceLicenseCatalogAsset catalog,
        ResourceLicenseEntry entry
    ) {
        if (entry.asset is null) throw new InvalidOperationException("Select an imported resource before recording its license.");
        var entries = catalog.entries.Where(item => item.asset?.identity.persistentId != entry.asset.identity.persistentId).Select(static item => item.Copy()).Append(entry.Copy()).ToArray();
        ValidateEntries(catalog.title, entries); catalog.entries = entries;
    }
    /// <summary>Removes a provenance record without changing or deleting the resource.</summary>
    /// <param name="catalog">The detached source document.</param>
    /// <param name="assetId">The native persistent resource identity.</param>
    public static void Remove(
        ResourceLicenseCatalogAsset catalog,
        Guid assetId
    ) => catalog.entries = catalog.entries.Where(item => item.asset?.identity.persistentId != assetId).ToArray();
}

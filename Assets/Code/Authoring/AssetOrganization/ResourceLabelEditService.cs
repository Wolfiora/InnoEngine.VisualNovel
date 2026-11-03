using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Authoring.Diagnostics;

namespace Inno.VisualNovel.Authoring.AssetOrganization;

/// <summary>Applies bounded annotation edits without owning asset import, identity, source paths or discovery.</summary>
public static class ResourceLabelEditService
{
    /// <summary>Validates canonical source declarations, permitting identified Missing references for repair.</summary>
    /// <param name="catalog">The complete proposed native annotation document.</param>
    public static void Validate(ResourceLabelCatalogAsset catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog); ValidateEntries(catalog.title, catalog.entries);
    }
    /// <summary>Adds or replaces one detached annotation, preserving existing row order and the underlying asset.</summary>
    /// <param name="catalog">The caller's detached native document.</param>
    /// <param name="entry">Complete proposed notes; surrounding group/tag whitespace and duplicate tags are normalized.</param>
    public static void Set(
        ResourceLabelCatalogAsset catalog,
        ResourceLabelEntry entry
    ) {
        Validate(catalog); ArgumentNullException.ThrowIfNull(entry);
        var value = Normalize(entry);
        var entries = catalog.entries.Select(item => item.asset?.identity.persistentId == value.asset?.identity.persistentId ? value : item.Copy()).ToArray();
        if (!entries.Any(item => item.asset?.identity.persistentId == value.asset?.identity.persistentId)) entries = [.. entries, value];
        ValidateEntries(catalog.title, entries); catalog.entries = entries;
    }
    /// <summary>Prepares every selected record before committing a single complete annotation array.</summary>
    /// <param name="catalog">The caller's detached native document.</param>
    /// <param name="assetIds">One to 10000 distinct existing native identities; stale targets reject the entire batch.</param>
    /// <param name="change">Explicit category, tag and optional description changes.</param>
    public static void ApplyBatch(
        ResourceLabelCatalogAsset catalog,
        IEnumerable<Guid> assetIds,
        ResourceLabelBatchChange change
    ) {
        Validate(catalog); ArgumentNullException.ThrowIfNull(assetIds); ArgumentNullException.ThrowIfNull(change);
        var ids = assetIds.Take(10001).ToArray(); var selected = ids.ToHashSet();
        var known = catalog.entries.Select(static entry => entry.asset!.identity.persistentId).ToHashSet();
        if (ids.Length is < 1 or > 10000 || selected.Count != ids.Length || selected.Contains(Guid.Empty)
            || !selected.IsSubsetOf(known))
            throw new AuthoringValidationException($"A batch requires 1–10000 distinct resource records that still exist.");
        string[] add = NormalizeTags(change.addTags); var remove = NormalizeTags(change.removeTags).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var entries = catalog.entries.Select(entry =>
        {
            var value = entry.Copy();
            if (!selected.Contains(value.asset!.identity.persistentId)) return value;
            if (change.replaceGroup) value.group = change.group;
            if (change.replaceDescription) value.description = change.description;
            value.tags = value.tags.Where(tag => !remove.Contains(tag)).Concat(add).ToArray(); return Normalize(value);
        }).ToArray();
        ValidateEntries(catalog.title, entries); catalog.entries = entries;
    }
    /// <summary>Returns independent matching annotations using native identity/path and creator-owned text.</summary>
    /// <param name="catalog">The native annotation document.</param>
    /// <param name="query">A literal case-insensitive substring, up to 4096 characters.</param>
    /// <param name="group">Null for every category; empty for ungrouped resources; otherwise an exact category.</param>
    /// <returns>Matching detached records in authored order; resource references remain canonical.</returns>
    public static IReadOnlyList<ResourceLabelEntry> Search(
        ResourceLabelCatalogAsset catalog,
        string query,
        string? group = null
    ) {
        Validate(catalog); ArgumentNullException.ThrowIfNull(query);
        if (query.Length > 4096) throw new ArgumentOutOfRangeException(nameof(query));
        string text = query.Trim();
        return Array.AsReadOnly(catalog.entries.Where(entry => (group is null || entry.group.Equals(group, StringComparison.OrdinalIgnoreCase))
            && (text.Length == 0 || Match(entry.asset!.assetPath.ToString()) || Match(entry.asset.identity.persistentId.ToString("D"))
                || Match(entry.group) || Match(entry.description) || entry.tags.Any(Match))).Select(static entry => entry.Copy()).ToArray());
        bool Match(string value) => value.Contains(text, StringComparison.OrdinalIgnoreCase);
    }
    /// <summary>Removes annotation metadata without deleting, moving or modifying its native resource.</summary>
    /// <param name="catalog">The caller's detached native document.</param>
    /// <param name="assetId">An existing annotated resource identity.</param>
    public static void Remove(
        ResourceLabelCatalogAsset catalog,
        Guid assetId
    ) {
        Validate(catalog);
        if (!catalog.entries.Any(entry => entry.asset!.identity.persistentId == assetId)) throw new AuthoringValidationException($"The resource annotation was removed. Refresh the list.");
        catalog.entries = catalog.entries.Where(entry => entry.asset!.identity.persistentId != assetId).Select(static entry => entry.Copy()).ToArray();
    }
    private static ResourceLabelEntry Normalize(ResourceLabelEntry entry)
    {
        if (entry.group is null || entry.description is null) throw new AuthoringValidationException($"Categories and descriptions require valid text. Use an empty string to clear them.");
        return new() { asset = entry.asset, group = entry.group.Trim(), tags = NormalizeTags(entry.tags), description = entry.description };
    }
    private static string[] NormalizeTags(string[] tags)
    {
        if (tags is null || tags.Length > 128 || tags.Any(static tag => tag is null)) throw new AuthoringValidationException($"Tag input is limited to 128 entries per batch; tags cannot be null.");
        var values = tags.Select(static tag => tag.Trim()).Where(static tag => tag.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (values.Length > 32 || values.Any(static tag => tag.Length > 64 || tag.Any(char.IsControl)))
            throw new AuthoringValidationException($"Each resource supports up to 32 tags. Each tag is limited to 64 characters without control characters.");
        return values;
    }
    private static void ValidateEntries(
        string title,
        ResourceLabelEntry[] entries
    ) {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 256 || entries is null || entries.Length > 10000)
            throw new AuthoringValidationException($"Resource annotations require a 1–256 character title and at most 10000 records per document.");
        var ids = new HashSet<Guid>();
        foreach (var entry in entries)
        {
            if (entry?.asset is null || entry.asset is ResourceLabelCatalogAsset || entry.asset.identity.persistentId == Guid.Empty || !ids.Add(entry.asset.identity.persistentId))
                throw new AuthoringValidationException($"Annotations require unique native resource identities and cannot reference annotation catalogs.");
            var normalized = Normalize(entry);
            if (entry.group.Length > 128 || entry.group.Any(char.IsControl) || entry.description.Length > 4096
                || entry.group != normalized.group || !entry.tags.SequenceEqual(normalized.tags, StringComparer.Ordinal))
                throw new AuthoringValidationException($"Categories are limited to 128 characters and descriptions to 4096. Source tags must be unique and trimmed.");
        }
    }
}

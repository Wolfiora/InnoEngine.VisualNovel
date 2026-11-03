using System;
using System.Linq;
using Inno.VisualNovel.Model.Collections;
using Inno.VisualNovel.Authoring.Diagnostics;

namespace Inno.VisualNovel.Authoring.CollectionEditing;

/// <summary>Applies collection metadata edits independently of ImGui, using stable identities and detached declarations.</summary>
public static class CollectionEditService
{
    /// <summary>Appends one independently identified collection item.</summary>
    /// <param name="asset">The detached native document.</param>
    /// <param name="id">An explicit unique content identity.</param>
    /// <param name="kind">The authored category.</param>
    public static void Add(
        CollectionAsset asset,
        string id,
        CollectionKind kind
    ) {
        if (string.IsNullOrWhiteSpace(id) || asset.entries.Any(item => item.id == id)) throw new AuthoringValidationException($"The collection entry ID is empty or already exists.");
        asset.entries = [.. asset.entries, new() { id = id, kind = kind, title = "New collection entry" }];
    }
    /// <summary>Replaces only the selected declaration while preserving its stable content identity.</summary>
    /// <param name="asset">The detached document.</param>
    /// <param name="entry">The edited declaration.</param>
    public static void Replace(
        CollectionAsset asset,
        CollectionEntry entry
    ) {
        int index = Array.FindIndex(asset.entries, item => item.id == entry.id);
        if (index < 0) throw new AuthoringValidationException($"The collection entry has been removed.");
        asset.entries[index] = CollectionEntryCopy.Copy(entry);
    }
    /// <summary>Removes an explicitly confirmed entry; unresolved story references remain visible to project validation.</summary>
    /// <param name="asset">The detached document.</param>
    /// <param name="id">The selected content identity.</param>
    public static void Remove(
        CollectionAsset asset,
        string id
    ) {
        if (!asset.entries.Any(item => item.id == id)) throw new AuthoringValidationException($"The collection entry has been removed.");
        asset.entries = asset.entries.Where(item => item.id != id).ToArray();
    }
}

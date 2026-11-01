using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Collections;

namespace Inno.VisualNovel.Authoring.CollectionEditing;

/// <summary>Edits CG variants atomically inside the caller's existing collection document transaction.</summary>
public static class CollectionVariantEditService
{
    /// <summary>Adds or replaces one stable variant after validating the complete owner and its declared gates.</summary>
    /// <param name="entry">The transaction-owned collection entry.</param>
    /// <param name="variant">The independently copied candidate metadata.</param>
    /// <param name="unlockIds">The current library's valid collection identities.</param>
    public static void Set(
        CollectionEntry entry,
        GalleryVariant variant,
        IReadOnlySet<string> unlockIds
    ) {
        var candidate = CollectionEntryCopy.Copy(entry);
        int index = Array.FindIndex(candidate.variants, value => value.id == variant.id);
        if (index < 0) candidate.variants = [.. candidate.variants, variant.Copy()];
        else candidate.variants[index] = variant.Copy();
        GalleryVariantRules.Validate(candidate, unlockIds); entry.variants = candidate.variants;
    }

    /// <summary>Reorders one variant without changing its identity or unlock relationship.</summary>
    /// <param name="entry">The transaction-owned entry.</param>
    /// <param name="id">The stable variant identity.</param>
    /// <param name="direction">The requested direction; only its sign is used.</param>
    public static void Move(
        CollectionEntry entry,
        string id,
        int direction
    ) {
        int source = Array.FindIndex(entry.variants, variant => variant.id == id);
        if (source < 0) throw new InvalidOperationException("The selected CG variant no longer exists: " + id);
        int target = Math.Clamp(source + Math.Sign(direction), 0, entry.variants.Length - 1);
        var values = entry.variants.ToArray(); (values[source], values[target]) = (values[target], values[source]); entry.variants = values;
    }

    /// <summary>Removes one author-selected variant; the parent entry and its global unlock identity remain intact.</summary>
    /// <param name="entry">The transaction-owned entry.</param>
    /// <param name="id">The exact variant identity.</param>
    public static void Remove(
        CollectionEntry entry,
        string id
    ) {
        if (!entry.variants.Any(variant => variant.id == id)) throw new InvalidOperationException("The selected CG variant no longer exists: " + id);
        entry.variants = entry.variants.Where(variant => variant.id != id).ToArray();
    }
}

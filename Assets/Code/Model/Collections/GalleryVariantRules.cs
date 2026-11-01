using System;
using System.Collections.Generic;
using System.Linq;

namespace Inno.VisualNovel.Model.Collections;

/// <summary>Validates ordered CG variant identities, artwork and explicit collection progress gates.</summary>
public static class GalleryVariantRules
{
    /// <summary>Rejects missing images, unknown gates, duplicate identities and unsupported categories before publication.</summary>
    /// <param name="entry">The owning CG metadata.</param>
    /// <param name="unlockIds">All declared collection identities in this game library.</param>
    public static void Validate(
        CollectionEntry entry,
        IReadOnlySet<string> unlockIds
    ) {
        if (entry.variants.Length > 256 || entry.kind != CollectionKind.Cg && entry.variants.Length > 0
            || entry.variants.Any(variant => variant is null || string.IsNullOrWhiteSpace(variant.id) || variant.id.Length > 128 || variant.title.Length > 512
                || variant.image is not { isMissing: false } || variant.unlockEntryId.Length > 0 && !unlockIds.Contains(variant.unlockEntryId))
            || entry.variants.Select(static variant => variant.id).Distinct(StringComparer.Ordinal).Count() != entry.variants.Length)
            throw new InvalidOperationException("CG variants require unique bounded identities, available images and declared unlock gates: " + entry.id);
    }
}

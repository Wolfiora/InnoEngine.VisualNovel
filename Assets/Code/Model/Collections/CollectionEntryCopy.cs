using System;
using System.Linq;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Model.Collections;

/// <summary>Copies collection declarations without retaining mutable authoring expressions.</summary>
public static class CollectionEntryCopy
{
    /// <summary>Freezes neutral metadata while borrowing the canonical imported artwork.</summary>
    /// <param name="entry">The source declaration.</param>
    /// <returns>A detached entry.</returns>
    public static CollectionEntry Copy(CollectionEntry entry) => new()
    {
        id = entry.id, kind = entry.kind, title = entry.title, description = entry.description, author = entry.author, image = entry.image,
        variants = entry.variants.Select(static variant => variant.Copy()).ToArray(),
        musicCueId = entry.musicCueId, chapterId = entry.chapterId, entryId = entry.entryId, initiallyUnlocked = entry.initiallyUnlocked, unlockCondition = Expression(entry.unlockCondition),
        initialValues = entry.initialValues.Select(static item => new CollectionVariable { id = item.id, value = item.value.Copy() }).ToArray()
    };
    private static ExpressionDocument Expression(
        ExpressionDocument source,
        int depth = 0
    ) => depth <= 64 ? new() { operation = source.operation, variableId = source.variableId, literal = source.literal.Copy(), operands = source.operands.Select(item => Expression(item, depth + 1)).ToArray() }
        : throw new InvalidOperationException("Collection unlock condition exceeds its expression depth budget.");
}

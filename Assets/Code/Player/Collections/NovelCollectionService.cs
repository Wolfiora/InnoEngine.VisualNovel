using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Collections;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Narrative.Values;

namespace Inno.VisualNovel.Player.Collections;

/// <summary>Owns monotonic unlock progress independently of ordinary saves, rollback and replay sessions.</summary>
public sealed class NovelCollectionService
{
    private readonly Dictionary<string, CollectionEntry> m_entries;
    private HashSet<string> m_unlocked;
    /// <summary>Gets an owner-local counter changed by committed unlock progress.</summary>
    public long revision { get; private set; }
    /// <summary>Freezes content and initializes only explicitly available items.</summary>
    /// <param name="asset">The optional imported collection library.</param>
    public NovelCollectionService(CollectionAsset? asset)
    {
        if (asset is { isMissing: true }) throw new InvalidOperationException("The collection asset is missing.");
        var entries = asset?.entries ?? [];
        if (entries.Length > 10000 || entries.Any(static entry => string.IsNullOrWhiteSpace(entry.id) || !Enum.IsDefined(entry.kind) || entry.image is { isMissing: true }
            || ExpressionOperations.UsesRandom(entry.unlockCondition))) throw new InvalidOperationException("Collections require valid identities, pure conditions and available artwork within the 10000-item budget.");
        var unlockIds = entries.Select(static entry => entry.id).ToHashSet(StringComparer.Ordinal);
        foreach (var entry in entries) GalleryVariantRules.Validate(entry, unlockIds);
        m_entries = entries.ToDictionary(static entry => entry.id, CollectionEntryCopy.Copy, StringComparer.Ordinal);
        m_unlocked = entries.Where(static entry => entry.initiallyUnlocked).Select(static entry => entry.id).ToHashSet(StringComparer.Ordinal);
    }
    /// <summary>Gets detached metadata in authoring order.</summary>
    public IReadOnlyList<CollectionEntry> entries => m_entries.Values.Select(CollectionEntryCopy.Copy).ToArray();
    /// <summary>Gets the latest source-addressed condition diagnostic, empty when healthy.</summary>
    public string error { get; private set; } = string.Empty;
    /// <summary>Reports whether content is available in this independent progress scope.</summary>
    /// <param name="id">The declared unlock identity.</param>
    /// <returns>True for an available item.</returns>
    public bool IsUnlocked(string id) => m_unlocked.Contains(id);
    /// <summary>Unlocks a declared entry monotonically.</summary>
    /// <param name="id">The known collection identity.</param>
    /// <returns>Whether this call unlocked it for the first time.</returns>
    public bool Unlock(string id)
    {
        if (!m_entries.ContainsKey(id)) throw new InvalidOperationException("Collection entry is unavailable: " + id);
        bool added = m_unlocked.Add(id); if (added) revision++; return added;
    }
    /// <summary>Reads one detached entry; locked content must be checked before presenting it.</summary>
    /// <param name="id">The stable content identity.</param>
    /// <returns>Detached metadata.</returns>
    public CollectionEntry Get(string id) => CollectionEntryCopy.Copy(m_entries.TryGetValue(id, out var entry) ? entry : throw new InvalidOperationException("Collection entry is unavailable: " + id));
    /// <summary>Evaluates pure automatic unlock rules without allowing random state or external side effects.</summary>
    /// <param name="variables">The current game's typed variable reader.</param>
    public void Evaluate(Func<string, NovelValue> variables)
    {
        error = string.Empty;
        foreach (var entry in m_entries.Values.Where(entry => !m_unlocked.Contains(entry.id)))
            try
            {
                NovelValue result = new ExpressionEvaluator(variables).Evaluate(entry.unlockCondition);
                if (result.kind != NovelValueKind.Boolean) throw new InvalidOperationException("Unlock condition must produce Boolean.");
                if (result.boolean) Unlock(entry.id);
            }
            catch (Exception failure) when (failure is not OutOfMemoryException) { error = entry.id + ": " + failure.Message; }
    }
    /// <summary>Captures global unlock identities in deterministic order.</summary>
    /// <returns>A detached monotonic progress set.</returns>
    public string[] Capture() => m_unlocked.Order(StringComparer.Ordinal).ToArray();
    /// <summary>Validates explicit loaded progress before replacing this collection scope.</summary>
    /// <param name="ids">The matching game's saved unlock identities.</param>
    public void Restore(string[] ids)
    {
        ValidateProgress(ids);
        var candidate = ids.ToHashSet(StringComparer.Ordinal); candidate.UnionWith(m_entries.Values.Where(static entry => entry.initiallyUnlocked).Select(static entry => entry.id));
        if (!m_unlocked.SetEquals(candidate)) { m_unlocked = candidate; revision++; }
    }
    /// <summary>Validates saved identities without changing active progress.</summary>
    /// <param name="ids">The candidate global unlock set.</param>
    public void ValidateProgress(string[] ids)
    {
        if (ids.Length > m_entries.Count || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length || ids.Any(id => !m_entries.ContainsKey(id)))
            throw new InvalidOperationException("Collection progress contains unknown or duplicated identities.");
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Authoring.Preview;

/// <summary>Contains a prepared project overlay and a frozen manifest of its native source inputs.</summary>
public sealed class PreviewProjectSnapshot
{
    private readonly IReadOnlyDictionary<Guid, VariableTableAsset> m_variableTables;
    internal PreviewProjectSnapshot(
        NovelProjectAsset project,
        IEnumerable<PreviewDraftSource> sources,
        IReadOnlyDictionary<Guid, VariableTableAsset> variableTables
    ) { this.project = project; this.sources = Array.AsReadOnly(sources.ToArray()); m_variableTables = variableTables.ToDictionary(static item => item.Key, static item => item.Value.Copy()); }
    /// <summary>Gets the detached game definition with dirty VN resources and compiled dirty chapter programs.</summary>
    public NovelProjectAsset project { get; }
    /// <summary>Gets native resource identities and unsaved-input provenance in stable source order.</summary>
    public IReadOnlyList<PreviewDraftSource> sources { get; }
    /// <summary>Gets the number of existing shared dirty documents used without opening additional documents.</summary>
    public int unsavedCount => sources.Count(static source => source.unsaved);
    /// <summary>Reads captured library values by native source identity without retaining later canonical or document changes.</summary>
    /// <param name="assetId">The chapter's native library identity, empty when unassigned.</param>
    /// <returns>An independent captured library, or null when the chapter has no library.</returns>
    public VariableTableAsset? GetVariableTable(Guid assetId) => assetId == Guid.Empty ? null
        : m_variableTables.TryGetValue(assetId, out var table) ? table.Copy() : throw new InvalidOperationException("The preview did not capture its shared variable library: " + assetId);
}

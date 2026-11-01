using System;
using System.Collections.Generic;
using System.Linq;

namespace Inno.VisualNovel.Authoring.VoiceManagement;

/// <summary>Owns an immutable voice-match review tied to one exact chapter's dialogue content.</summary>
public sealed class VoiceBatchPreview
{
    /// <summary>Freezes voice matches without retaining live assets, editor owners or caller-owned arrays.</summary>
    /// <param name="chapter">The source chapter identity.</param>
    /// <param name="baseline">The reviewed dialogue fingerprint.</param>
    /// <param name="rows">All dialogue rows, including unavailable and preserved bindings.</param>
    /// <param name="language">The exact recording language.</param>
    /// <param name="localized">Whether bindings target a translation table rather than source instructions.</param>
    /// <param name="characterNames">Optional frozen names for linked characters used by this review.</param>
    public VoiceBatchPreview(
        string chapter,
        string baseline,
        IEnumerable<VoiceBatchRow> rows,
        string language,
        bool localized,
        IReadOnlyDictionary<string, string>? characterNames = null
    ) { this.chapter = chapter; this.baseline = baseline; this.rows = Array.AsReadOnly(rows.ToArray()); this.language = language; this.localized = localized;
        this.characterNames = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>((characterNames ?? new Dictionary<string, string>()).ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal)); }
    /// <summary>Gets the source chapter identity.</summary>
    public string chapter { get; }
    /// <summary>Gets the source fingerprint required by an atomic binding edit.</summary>
    public string baseline { get; }
    /// <summary>Gets all reviewed matches in finite script projection order.</summary>
    public IReadOnlyList<VoiceBatchRow> rows { get; }
    /// <summary>Gets the recording language captured during review.</summary>
    public string language { get; }
    /// <summary>Gets whether ready matches update language-specific voice bindings.</summary>
    public bool localized { get; }
    /// <summary>Gets the linked default character names frozen during the recording review.</summary>
    public IReadOnlyDictionary<string, string> characterNames { get; }
}

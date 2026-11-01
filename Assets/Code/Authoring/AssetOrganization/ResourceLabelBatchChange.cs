namespace Inno.VisualNovel.Authoring.AssetOrganization;

/// <summary>Describes one explicit batch annotation edit for the caller's existing native History transaction.</summary>
public sealed class ResourceLabelBatchChange
{
    /// <summary>Gets whether the group is explicitly replaced, including clearing it with an empty value.</summary>
    public bool replaceGroup { get; init; }
    /// <summary>Gets the new group when replacement is enabled.</summary>
    public string group { get; init; } = string.Empty;
    /// <summary>Gets labels appended after removals, deduplicated without changing the spelling of retained labels.</summary>
    public string[] addTags { get; init; } = [];
    /// <summary>Gets labels removed by an ordinal case-insensitive comparison before additions.</summary>
    public string[] removeTags { get; init; } = [];
    /// <summary>Gets whether descriptions are explicitly replaced for all selected resources.</summary>
    public bool replaceDescription { get; init; }
    /// <summary>Gets the replacement description when enabled; otherwise each resource retains its own notes.</summary>
    public string description { get; init; } = string.Empty;
}

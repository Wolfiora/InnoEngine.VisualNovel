using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InnoEngine.Assets;

namespace Inno.VisualNovel.Authoring.Publishing;

/// <summary>Distinguishes rejected content, unverified host contracts and non-blocking authoring advice.</summary>
public enum PublishIssueLevel
{
    /// <summary>Content cannot be published correctly.</summary>
    Error,
    /// <summary>A required host capability still needs independent verification.</summary>
    Pending,
    /// <summary>Content remains usable but deserves review.</summary>
    Warning
}

/// <summary>Locates one actionable preflight result without retaining a live document or delegate.</summary>
/// <param name="level">The release impact.</param>
/// <param name="code">The stable diagnostic code.</param>
/// <param name="location">The asset, language or instruction location.</param>
/// <param name="message">The reason and next authoring action.</param>
public sealed record PublishIssue(
    PublishIssueLevel level,
    string code,
    string location,
    string message
);

/// <summary>Records one deduplicated native dependency and all directly referring assets.</summary>
/// <param name="id">The engine persistent asset identity.</param>
/// <param name="path">The canonical source mount and path.</param>
/// <param name="propertyBytes">The native serialized property size, excluding imported artifact data.</param>
/// <param name="referrers">Canonical assets directly referring to this identity.</param>
public sealed record PublishAsset(
    Guid id,
    AssetPath path,
    int propertyBytes,
    IReadOnlyList<string> referrers
);

/// <summary>Owns an immutable content audit, explicitly separate from an actual engine build result.</summary>
public sealed class NovelPublishReport
{
    /// <summary>Creates an independently owned audit snapshot.</summary>
    /// <param name="title">The author-facing game title.</param>
    /// <param name="fingerprint">The importer content identity at inspection time.</param>
    /// <param name="assets">The deduplicated native content closure.</param>
    /// <param name="issues">Errors, unverified requirements and advice.</param>
    public NovelPublishReport(
        string title,
        string fingerprint,
        IEnumerable<PublishAsset> assets,
        IEnumerable<PublishIssue> issues
    ) {
        this.title = title; this.fingerprint = fingerprint;
        this.assets = Array.AsReadOnly(assets.Select(static asset => asset with { referrers = Array.AsReadOnly(asset.referrers.ToArray()) }).ToArray());
        this.issues = Array.AsReadOnly(issues.ToArray());
    }
    /// <summary>Gets the audited title.</summary>
    public string title { get; }
    /// <summary>Gets the exact imported build identity, empty for an unsaved definition.</summary>
    public string fingerprint { get; }
    /// <summary>Gets the native dependency closure without duplicate identities.</summary>
    public IReadOnlyList<PublishAsset> assets { get; }
    /// <summary>Gets the source-located audit results.</summary>
    public IReadOnlyList<PublishIssue> issues { get; }
    /// <summary>Gets whether no known content error prevents opening the native export workflow.</summary>
    public bool contentValid => !issues.Any(static item => item.level == PublishIssueLevel.Error);
    /// <summary>Gets whether required host verification remains outstanding.</summary>
    public bool requiresHostVerification => issues.Any(static item => item.level == PublishIssueLevel.Pending);
    /// <summary>Formats an auditable Chinese report without describing preflight as a completed build.</summary>
    /// <returns>A Markdown report suitable for the project's artifacts directory.</returns>
    public string ToMarkdown()
    {
        var text = new StringBuilder("# VN publishing preflight\n\n");
        text.Append("Game: ").Append(Cell(title)).Append("\n\nContent fingerprint: ").Append(fingerprint.Length == 0 ? "Not generated" : fingerprint).Append("\n\n");
        text.Append(contentValid ? "Content validation passed." : "Content errors prevent publishing.");
        text.Append("This report does not perform a Windows game build, launch, graphics, audio or physical input acceptance.\n\n");
        foreach (PublishIssue item in issues)
            text.Append("- [").Append(item.level).Append("] ").Append(Cell(item.code)).Append(" · ").Append(Cell(item.location)).Append("：").Append(Cell(item.message)).Append('\n');
        text.Append("\nResources: ").Append(assets.Count).Append(". Property bytes measure native serialized data, not package size. The engine build report determines final assets, shaders and target capabilities.\n\n");
        text.Append("| Asset | Identity | Property bytes | Direct referrers |\n|---|---|---:|---|\n");
        foreach (PublishAsset item in assets)
            text.Append("| ").Append(Cell(item.path.ToString())).Append(" | ").Append(item.id).Append(" | ").Append(item.propertyBytes)
                .Append(" | ").Append(Cell(string.Join("；", item.referrers))).Append(" |\n");
        return text.ToString();
    }
    private static string Cell(string value) => value.Replace("|", "\\|", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}

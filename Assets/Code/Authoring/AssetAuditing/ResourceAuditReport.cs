using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Inno.VisualNovel.Authoring.Publishing;

namespace Inno.VisualNovel.Authoring.AssetAuditing;

/// <summary>Freezes inventory, byte duplicates, native reference evidence and recorded attribution without changing source assets.</summary>
public sealed class ResourceAuditReport
{
    /// <summary>Copies a completed or partial owner-thread audit into an immutable report.</summary>
    /// <param name="title">The selected game title.</param>
    /// <param name="fingerprint">The imported game content identity at inspection time.</param>
    /// <param name="finished">Whether inventory enumeration ended.</param>
    /// <param name="cancelled">Whether the owner cancelled.</param>
    /// <param name="closureComplete">Whether preflight proved a valid native closure.</param>
    /// <param name="items">Completed source observations.</param>
    /// <param name="issues">Unavailable evidence, errors and provenance warnings.</param>
    /// <param name="external">Read-only plugin dependencies outside the project inventory.</param>
    /// <param name="licenses">Recorded notices for resources included in the selected game's closure.</param>
    public ResourceAuditReport(
        string title,
        string fingerprint,
        bool finished,
        bool cancelled,
        bool closureComplete,
        IEnumerable<ResourceAuditItem> items,
        IEnumerable<PublishIssue> issues,
        IEnumerable<PublishAsset> external,
        IEnumerable<ResourceLicenseInfo> licenses
    ) {
        this.title = title; this.fingerprint = fingerprint; this.finished = finished; this.cancelled = cancelled; this.closureComplete = closureComplete;
        this.items = Array.AsReadOnly(items.OrderBy(static item => item.path.ToString(), StringComparer.Ordinal).Select(static item => item with { referrers = Array.AsReadOnly(item.referrers.ToArray()) }).ToArray());
        this.issues = Array.AsReadOnly(issues.ToArray()); this.external = Array.AsReadOnly(external.Select(static item => item with { referrers = Array.AsReadOnly(item.referrers.ToArray()) }).ToArray());
        this.licenses = Array.AsReadOnly(licenses.ToArray());
        duplicateGroups = Array.AsReadOnly(this.items.Where(static item => item.hash.Length > 0).GroupBy(static item => (item.bytes, item.hash)).Where(static group => group.Count() > 1)
            .Select(static group => (IReadOnlyList<ResourceAuditItem>)Array.AsReadOnly(group.ToArray())).ToArray());
    }
    /// <summary>Gets the selected game title.</summary>
    public string title { get; }
    /// <summary>Gets the imported content identity.</summary>
    public string fingerprint { get; }
    /// <summary>Gets whether enumeration ended; errors and limits may still make the report incomplete.</summary>
    public bool finished { get; }
    /// <summary>Gets whether the owner cancelled the scan.</summary>
    public bool cancelled { get; }
    /// <summary>Gets whether a valid native game dependency closure was available.</summary>
    public bool closureComplete { get; }
    /// <summary>Gets whether inventory ended without cancellation or a source/error budget failure.</summary>
    public bool inventoryComplete => finished && !cancelled && !issues.Any(static issue => issue.level == PublishIssueLevel.Error);
    /// <summary>Gets immutable source observations.</summary>
    public IReadOnlyList<ResourceAuditItem> items { get; }
    /// <summary>Gets errors, provenance gaps and unavailable-evidence explanations.</summary>
    public IReadOnlyList<PublishIssue> issues { get; }
    /// <summary>Gets byte-identical unchanged source groups; they are not automatic deletion candidates.</summary>
    public IReadOnlyList<IReadOnlyList<ResourceAuditItem>> duplicateGroups { get; }
    /// <summary>Gets read-only plugin dependencies outside this project's source inventory.</summary>
    public IReadOnlyList<PublishAsset> external { get; }
    /// <summary>Gets author-recorded provenance for included native identities.</summary>
    public IReadOnlyList<ResourceLicenseInfo> licenses { get; }
    /// <summary>Exports an auditable snapshot without presenting source byte size as final package size.</summary>
    /// <returns>A Chinese Markdown report.</returns>
    public string ToMarkdown()
    {
        var text = new StringBuilder("# VN resource audit\n\n");
        text.Append("Game: ").Append(Cell(title)).Append("\n\nBuild fingerprint: ").Append(Cell(fingerprint)).Append("\n\n");
        text.Append(inventoryComplete ? "Project source enumeration completed." : "Incomplete report: scanning, cancelled, or a read or budget error occurred.");
        text.Append(closureComplete ? " The native content closure is available." : " The native content closure is incomplete; unused status cannot be inferred.");
        text.Append("\n\nScope: known VN/PNG/JPEG/WAV/TTF/OTF sources in this project's Assets and other formats in the native closure. Sample, hidden and reparse subtrees are excluded. Unused means unreferenced by the selected game; other scenes, games or dynamic scripts may still need the resource. This tool does not delete files.\n\n");
        text.Append("Source bytes are not Player package size. Equal bytes do not make asset identities or uses interchangeable. License records are author-provided; this audit does not verify permissions or the contents of the final distribution.\n\n");
        foreach (var issue in issues) text.Append("- [").Append(issue.level).Append("] ").Append(Cell(issue.code)).Append(" · ").Append(Cell(issue.location)).Append("：").Append(Cell(issue.message)).Append('\n');
        text.Append("\n| Resource source | Native identity | Source bytes | Reference status | Direct referrers | Information |\n|---|---|---:|---|---|---|\n");
        foreach (var item in items) text.Append("| ").Append(Cell(item.path.ToString())).Append(" | ").Append(item.id).Append(" | ").Append(item.bytes).Append(" | ").Append(ResourceInventoryRules.DescribeUse(item.use))
            .Append(" | ").Append(Cell(string.Join("；", item.referrers))).Append(" | ").Append(Cell(item.message)).Append(" |\n");
        text.Append("\n## Duplicate source bytes\n\n");
        foreach (var group in duplicateGroups)
        {
            text.Append("- SHA-256 ").Append(group[0].hash).Append(" · Per item: ").Append(group[0].bytes).Append(" bytes\n");
            foreach (var item in group) text.Append("  - ").Append(Cell(item.path.ToString())).Append('\n');
        }
        text.Append("\n## Read-only plugin references\n\nThese references are excluded from project cleanup candidates. Check game distribution requirements against the included licenses; installing a plugin does not fulfill attribution requirements.\n\n");
        foreach (var item in external) text.Append("- ").Append(Cell(item.path.ToString())).Append(" · ").Append(item.id).Append('\n');
        text.Append("\n## Recorded distribution information\n\n");
        foreach (var item in licenses)
        {
            text.Append("- ").Append(item.assetId).Append(" · ").Append(Cell(item.author)).Append(" · ").Append(Cell(item.license)).Append(" · Source: ").Append(Cell(item.source))
                .Append(" · Redistribution confirmed: ").Append(item.redistributionConfirmed ? "Yes" : "No").Append(" · Modifications: ").Append(Cell(item.modifications)).Append('\n');
        }
        return text.ToString();
    }
    /// <summary>Exports recorded included-resource notices as plain text, retaining the supplied notice bodies.</summary>
    /// <returns>A notice draft for the author to review and include through the game publication workflow.</returns>
    public string NoticesText()
    {
        var text = new StringBuilder();
        foreach (var item in licenses)
        {
            text.Append(item.assetId).Append("\n").Append(item.author).Append("\n").Append(item.license).Append("\n").Append(item.source).Append("\n");
            text.Append(item.notice).Append("\n");
            if (item.modifications.Length > 0) text.Append("Modifications: ").Append(item.modifications).Append("\n");
            text.Append('\n');
        }
        return text.ToString();
    }
    private static string Cell(string value) => value.Replace("|", "\\|", StringComparison.Ordinal).Replace("[", "\\[", StringComparison.Ordinal).Replace("]", "\\]", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}

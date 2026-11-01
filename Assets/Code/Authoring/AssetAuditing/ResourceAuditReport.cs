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
        var text = new StringBuilder("# VN 资源审计\n\n");
        text.Append("游戏：").Append(Cell(title)).Append("\n\n构建指纹：").Append(Cell(fingerprint)).Append("\n\n");
        text.Append(inventoryComplete ? "项目源枚举完成。" : "报告不完整：仍在扫描、已取消或存在读取/上限错误。");
        text.Append(closureComplete ? " 原生内容闭包可用。" : " 原生内容闭包不完整，不能推断未引用。");
        text.Append("\n\n范围为当前项目 Assets 下已知 VN/PNG/JPEG/WAV/TTF/OTF 源及原生闭包中的其他格式；跳过 ~、隐藏和重解析子树。未引用仅相对于选定游戏，不证明其他场景、游戏或动态脚本不需要该资源。本工具不删除文件。\n\n");
        text.Append("源文件字节不等于 Player 包体积；字节相同也不证明资产身份或用法可互换。许可内容由作者填写，审计不验证法律授权或最终分发是否携带全部要求的文件。\n\n");
        foreach (var issue in issues) text.Append("- [").Append(issue.level).Append("] ").Append(Cell(issue.code)).Append(" · ").Append(Cell(issue.location)).Append("：").Append(Cell(issue.message)).Append('\n');
        text.Append("\n| 资源源文件 | 原生身份 | 源字节 | 引用状态 | 直接引用者 | 信息 |\n|---|---|---:|---|---|---|\n");
        foreach (var item in items) text.Append("| ").Append(Cell(item.path.ToString())).Append(" | ").Append(item.id).Append(" | ").Append(item.bytes).Append(" | ").Append(ResourceInventoryRules.DescribeUse(item.use))
            .Append(" | ").Append(Cell(string.Join("；", item.referrers))).Append(" | ").Append(Cell(item.message)).Append(" |\n");
        text.Append("\n## 源文件字节重复\n\n");
        foreach (var group in duplicateGroups)
        {
            text.Append("- SHA-256 ").Append(group[0].hash).Append(" · 每项 ").Append(group[0].bytes).Append(" 字节\n");
            foreach (var item in group) text.Append("  - ").Append(Cell(item.path.ToString())).Append('\n');
        }
        text.Append("\n## 插件只读引用\n\n这些引用不进入项目源清理候选。按插件随附许可核对游戏分发要求，不能把“插件已安装”当作最终署名已完成。\n\n");
        foreach (var item in external) text.Append("- ").Append(Cell(item.path.ToString())).Append(" · ").Append(item.id).Append('\n');
        text.Append("\n## 已记录的分发信息\n\n");
        foreach (var item in licenses)
        {
            text.Append("- ").Append(item.assetId).Append(" · ").Append(Cell(item.author)).Append(" · ").Append(Cell(item.license)).Append(" · 来源：").Append(Cell(item.source))
                .Append(" · 已确认分发：").Append(item.redistributionConfirmed ? "是" : "否").Append(" · 修改：").Append(Cell(item.modifications)).Append('\n');
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

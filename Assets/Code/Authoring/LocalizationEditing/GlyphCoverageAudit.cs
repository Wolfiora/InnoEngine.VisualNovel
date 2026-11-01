using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Assets;
using InnoEngine.Text;
using Inno.VisualNovel.Model.Text;
using Inno.VisualNovel.UI.Text;
using Inno.VisualNovel.Authoring.Diagnostics;

namespace Inno.VisualNovel.Authoring.LocalizationEditing;

/// <summary>Runs cancellable owner-thread font coverage checks without creating an index or modifying game assets.</summary>
public sealed class GlyphCoverageAudit : IDisposable
{
    private readonly IEnumerator<GlyphCoverageDocument> m_sources;
    private readonly NovelTextLayoutBuilder m_layout;
    private readonly List<GlyphCoverageIssue> m_issues = [];
    /// <summary>Creates a check over frozen source contexts using the engine shaper or a deterministic test adapter.</summary>
    /// <param name="sources">Static sources enumerated on the owner thread.</param>
    /// <param name="shape">Optional adapter matching the native UTF-8 cluster contract.</param>
    public GlyphCoverageAudit(
        IEnumerable<GlyphCoverageDocument> sources,
        Func<FontAsset, string, TextStyle, TextLayout>? shape = null
    ) { m_sources = sources.GetEnumerator(); m_layout = new(shape); }
    /// <summary>Gets the completed source context count, including failed sources.</summary>
    public int processed { get; private set; }
    /// <summary>Gets how many sources contain variables whose runtime values cannot be proven statically.</summary>
    public int dynamicSources { get; private set; }
    /// <summary>Gets whether every enumerated context was checked or a declared result bound was reached.</summary>
    public bool completed { get; private set; }
    /// <summary>Gets whether the owner explicitly cancelled the check.</summary>
    public bool cancelled { get; private set; }
    /// <summary>Gets whether the result bound prevented a complete report.</summary>
    public bool truncated { get; private set; }
    /// <summary>Gets read-only source-addressed missing clusters and failed checks.</summary>
    public IReadOnlyList<GlyphCoverageIssue> issues => m_issues.AsReadOnly();
    /// <summary>Checks a bounded number of source contexts; native shaping itself remains a synchronous host operation.</summary>
    /// <param name="budget">One to 128 contexts for this update.</param>
    public void Step(int budget = 2)
    {
        if (budget is < 1 or > 128) throw new ArgumentOutOfRangeException(nameof(budget));
        for (int index = 0; index < budget && !completed && !cancelled; index++)
        {
            GlyphCoverageDocument? source = null;
            try
            {
                if (!m_sources.MoveNext()) { Finish(); break; }
                source = m_sources.Current; processed++;
                if (source.font is null || source.font.isMissing) throw new AuthoringValidationException($"当前文字没有可用默认字体。");
                string text = source.text;
                if (source.interpolated)
                {
                    var tokens = NovelTextTemplate.Parse(text);
                    if (tokens.Any(static token => token.variable)) dynamicSources++;
                    text = string.Concat(tokens.Where(static token => !token.variable).Select(static token => token.value));
                }
                var document = source.rich ? NovelTextMarkup.Parse(text) : NovelTextDocument.Literal(text);
                var layout = m_layout.Build(new(document), source.font, source.size, 16384, resources: source.resources);
                foreach (var group in layout.missingGlyphs.GroupBy(static missing => (missing.text, missing.ruby)))
                    Add(new(source.textId, source.location, source.assetPath, source.nodeId, group.First().sourceStart, group.Key.text, group.Count(),
                        group.Key.ruby ? "注音字素在全部字体中缺失。" : "静态文字字素在全部字体中缺失。", false, source.elementId));
            }
            catch (Exception failure) when (failure is not OutOfMemoryException)
            {
                Add(new(source?.textId ?? "", source?.location ?? "内容枚举", source?.assetPath ?? default, source?.nodeId ?? "", 0, "", 1, failure.Message, true, source?.elementId ?? ""));
                if (source is null) Finish();
            }
        }
    }
    /// <summary>Cancels without discarding already collected source-located results.</summary>
    public void Cancel() { if (completed || cancelled) return; cancelled = true; m_sources.Dispose(); }
    /// <inheritdoc />
    public void Dispose() => Cancel();
    private void Finish() { if (completed) return; completed = true; m_sources.Dispose(); }
    private void Add(GlyphCoverageIssue issue)
    {
        if (m_issues.Count < 10000) m_issues.Add(issue);
        else { truncated = true; Finish(); }
    }
}

/// <summary>Locates one grouped missing grapheme or an unavailable native shaping/context check.</summary>
/// <param name="textId">The stable text identity.</param>
/// <param name="location">The complete author-facing source context.</param>
/// <param name="assetPath">The native source asset.</param>
/// <param name="nodeId">The authored node identity, empty for UI.</param>
/// <param name="start">The first UTF-16 position within static text after omitting variable tokens.</param>
/// <param name="text">The complete missing grapheme, empty for a failed check.</param>
/// <param name="count">Its occurrence count within this context.</param>
/// <param name="message">The diagnostic explanation.</param>
/// <param name="failed">Whether coverage was unavailable instead of proving a missing glyph.</param>
/// <param name="elementId">The stable page element for an editable UI source.</param>
public sealed record GlyphCoverageIssue(string textId, string location, AssetPath assetPath, string nodeId, int start, string text, int count, string message, bool failed, string elementId = "");

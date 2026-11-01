using System;
using System.Linq;
using InnoEngine.Assets;
using Inno.VisualNovel.Authoring.Documents;

namespace Inno.VisualNovel.Authoring.Preview;

/// <summary>Detects changes to captured native preview inputs without decoding drafts, watching files or replacing the running session.</summary>
public sealed class PreviewFreshnessTracker
{
    private readonly StoryDocumentsModule m_stories;
    private readonly ResourceDocumentsModule m_resources;
    private readonly PreviewDraftSource[] m_sources;
    private readonly NativeDocumentStamp?[] m_documents;
    private readonly NativeDocumentStamp? m_story;
    private readonly NativeDocumentStamp? m_case;
    private int m_cursor;

    /// <summary>Captures the current editor generation's stamps immediately after preparing a preview.</summary>
    /// <param name="snapshot">The exact native resource closure used by the running candidate.</param>
    /// <param name="stories">The existing chapter document owner.</param>
    /// <param name="resources">The existing resource document owner.</param>
    /// <param name="storyDocument">An additional explicitly previewed chapter, or empty when unrelated to this preview.</param>
    /// <param name="caseDocument">The originating native preview case, or empty for a temporary case.</param>
    public PreviewFreshnessTracker(
        PreviewProjectSnapshot snapshot,
        StoryDocumentsModule stories,
        ResourceDocumentsModule resources,
        Guid storyDocument = default,
        Guid caseDocument = default
    ) {
        ArgumentNullException.ThrowIfNull(snapshot);
        m_stories = stories; m_resources = resources; m_sources = snapshot.sources.ToArray();
        m_documents = m_sources.Select(source => Stamp(AssetPath.Parse(source.path))).ToArray();
        m_story = Require(stories.GetStamp(storyDocument), storyDocument);
        m_case = Require(resources.GetStamp(caseDocument), caseDocument);
        for (int index = 0; index < m_sources.Length; index++)
            if (m_sources[index].unsaved != (m_documents[index]?.unsaved ?? false))
                Invalidate("预览准备后的文档状态已变化：" + m_sources[index].path);
    }
    /// <summary>Gets whether any captured input has changed; undo does not silently validate an old route again.</summary>
    public bool stale { get; private set; }
    /// <summary>Gets the first changed source suitable for an actionable author-facing message.</summary>
    public string reason { get; private set; } = string.Empty;
    /// <summary>Checks a bounded number of resource inputs and the explicitly previewed documents.</summary>
    /// <param name="sourceBudget">The maximum native asset lookups for one editor update, from one to 1024.</param>
    /// <returns>Whether the preview remains current among all inputs inspected so far.</returns>
    public bool Probe(int sourceBudget = 16)
    {
        if (sourceBudget is < 1 or > 1024) throw new ArgumentOutOfRangeException(nameof(sourceBudget));
        if (stale) return false;
        CheckOwnedDocuments();
        for (int count = 0; !stale && count < Math.Min(sourceBudget, m_sources.Length); count++)
        { CheckSource(m_cursor); m_cursor = (m_cursor + 1) % m_sources.Length; }
        return !stale;
    }
    /// <summary>Validates the complete captured closure immediately before writing recorded state back to an authored case.</summary>
    /// <returns>Whether every native source and originating document still matches the captured candidate.</returns>
    public bool CheckAll()
    {
        if (stale) return false;
        CheckOwnedDocuments();
        for (int index = 0; !stale && index < m_sources.Length; index++) CheckSource(index);
        return !stale;
    }
    /// <summary>Marks an owner-local temporary preview setting change that has no native document stamp.</summary>
    /// <param name="message">The reason the author must restart before recording or writing back.</param>
    public void Invalidate(string message)
    {
        if (stale) return;
        stale = true; reason = message;
    }
    private void CheckOwnedDocuments()
    {
        if (m_story is not null && m_stories.GetStamp(m_story.documentId) != m_story)
            Invalidate("预览章节已修改或关闭：" + m_story.path);
        if (m_case is not null && m_resources.GetStamp(m_case.documentId) != m_case)
            Invalidate("预览用例已修改或关闭：" + m_case.path);
    }
    private void CheckSource(int index)
    {
        PreviewDraftSource captured = m_sources[index];
        var current = Assets.Load<AssetObject>(captured.assetId);
        if (current.isMissing || current.contentVersion != captured.contentVersion || current.assetPath.ToString() != captured.path)
        { Invalidate("预览资源已重新导入、移动或缺失：" + captured.path); return; }
        var before = m_documents[index]; var after = Stamp(AssetPath.Parse(captured.path));
        bool changed = before?.documentId == after?.documentId ? before != after
            : before?.unsaved == true || after?.unsaved == true;
        if (changed) Invalidate("预览创作源已修改：" + captured.path);
    }
    private NativeDocumentStamp? Stamp(AssetPath path) => m_stories.GetStamp(path) ?? m_resources.GetStamp(path);
    private static NativeDocumentStamp? Require(
        NativeDocumentStamp? stamp,
        Guid documentId
    ) => documentId == Guid.Empty ? null : stamp ?? throw new InvalidOperationException("The preview's originating document is no longer open.");
}

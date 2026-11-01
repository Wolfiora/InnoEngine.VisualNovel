using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Assets;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Model.Project;

namespace Inno.VisualNovel.Authoring.StoryEditing;

/// <summary>Associates a finite source match with its authored chapter and exact inspected draft.</summary>
/// <param name="path">The native chapter path.</param>
/// <param name="chapterId">The current draft's stable chapter identity.</param>
/// <param name="chapterTitle">The current authored chapter title.</param>
/// <param name="match">The existing shared chapter-search match.</param>
/// <param name="fingerprint">The exact neutral draft identity used for stale-result detection.</param>
/// <param name="unsaved">Whether the result includes an open unsaved draft.</param>
public sealed record GameStorySearchResult(AssetPath path, string chapterId, string chapterTitle, StorySearchResult match, string fingerprint, bool unsaved);

/// <summary>Reports one source that could not be searched without hiding successful matches from other chapters.</summary>
/// <param name="path">The affected native source.</param>
/// <param name="message">The actionable import, access or search diagnostic.</param>
public sealed record GameStorySearchIssue(AssetPath path, string message);

/// <summary>Searches one explicit game's authored graphs incrementally without opening documents or maintaining a second content index.</summary>
public sealed class GameStorySearch : IDisposable
{
    private readonly StorySearchService m_search;
    private readonly Func<AssetPath, StoryDocumentSnapshot> m_inspect;
    private readonly AssetPath[] m_targets;
    private readonly List<GameStorySearchResult> m_results = [];
    private readonly List<GameStorySearchIssue> m_issues = [];
    private IEnumerator<StorySearchResult?>? m_cursor;
    private StoryDocumentSnapshot? m_current;
    private bool m_disposed;

    /// <summary>Freezes the chapter source list and query, while inspecting live dirty drafts only when their turn is reached.</summary>
    /// <param name="project">The selected game's explicit chapter roots.</param>
    /// <param name="search">The same chapter search used by graph and script authoring.</param>
    /// <param name="inspect">The document owner's read-only source/draft inspector.</param>
    /// <param name="query">A case-insensitive literal query, limited to 1024 characters.</param>
    /// <param name="bookmarksOnly">Whether only bookmarked authored nodes are eligible.</param>
    public GameStorySearch(
        NovelProjectAsset project,
        StorySearchService search,
        Func<AssetPath, StoryDocumentSnapshot> inspect,
        string query,
        bool bookmarksOnly = false
    ) : this(Paths(project), search, inspect, query, bookmarksOnly) { }

    /// <summary>Searches an explicit bounded set of native chapter paths using the current document owner's source policy.</summary>
    /// <param name="paths">The chapter paths to inspect once in their supplied order.</param>
    /// <param name="search">The shared node-content search implementation.</param>
    /// <param name="inspect">The owner-thread native source/draft callback.</param>
    /// <param name="query">The frozen literal query.</param>
    /// <param name="bookmarksOnly">Whether only bookmarks are eligible.</param>
    public GameStorySearch(
        IEnumerable<AssetPath> paths,
        StorySearchService search,
        Func<AssetPath, StoryDocumentSnapshot> inspect,
        string query,
        bool bookmarksOnly = false
    ) {
        ArgumentNullException.ThrowIfNull(paths); ArgumentNullException.ThrowIfNull(query);
        m_search = search ?? throw new ArgumentNullException(nameof(search)); m_inspect = inspect ?? throw new ArgumentNullException(nameof(inspect));
        m_targets = paths.Take(2049).ToArray();
        if (query.Length > 1024 || m_targets.Length > 2048) throw new ArgumentException("Game search requires a query within 1024 characters and at most 2048 authored chapters.");
        this.query = query; this.bookmarksOnly = bookmarksOnly;
        m_targets = m_targets.Distinct().ToArray();
        results = m_results.AsReadOnly(); issues = m_issues.AsReadOnly(); completed = m_targets.Length == 0;
    }
    /// <summary>Gets the frozen literal query.</summary>
    public string query { get; }
    /// <summary>Gets whether only bookmarks are included.</summary>
    public bool bookmarksOnly { get; }
    /// <summary>Gets accumulated immutable source matches; the collection grows only during Step.</summary>
    public IReadOnlyList<GameStorySearchResult> results { get; }
    /// <summary>Gets per-chapter failures without discarding other results.</summary>
    public IReadOnlyList<GameStorySearchIssue> issues { get; }
    /// <summary>Gets the number of selected native chapter paths.</summary>
    public int chapterCount => m_targets.Length;
    /// <summary>Gets the number of fully processed or diagnosed chapter paths.</summary>
    public int completedChapters { get; private set; }
    /// <summary>Gets the number of inspected nodes, including nonmatches.</summary>
    public long processedNodes { get; private set; }
    /// <summary>Gets whether no further sources or nodes will be processed.</summary>
    public bool completed { get; private set; }
    /// <summary>Gets whether the author cancelled this search.</summary>
    public bool cancelled { get; private set; }
    /// <summary>Gets whether the explicit 100000-result bound ended the search.</summary>
    public bool truncated { get; private set; }

    /// <summary>Processes bounded node work on the editor owner thread; source decoding occurs at most once per call.</summary>
    /// <param name="nodeBudget">The maximum nodes to inspect, between 1 and 4096.</param>
    public void Step(int nodeBudget = 128)
    {
        if (m_disposed) throw new ObjectDisposedException(nameof(GameStorySearch));
        if (nodeBudget is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(nodeBudget));
        if (completed) return;
        try
        {
            if (m_cursor is null)
            {
                m_current = m_inspect(m_targets[completedChapters]);
                m_cursor = m_search.Scan(m_current.draft, query, bookmarksOnly).GetEnumerator();
            }
            for (int index = 0; index < nodeBudget; index++)
            {
                if (!m_cursor.MoveNext()) { FinishChapter(); return; }
                processedNodes++;
                if (m_cursor.Current is not { } match) continue;
                if (m_results.Count >= 100000) { truncated = completed = true; Release(); return; }
                m_results.Add(new(m_current!.path, m_current.draft.chapterId, m_current.draft.title, match, m_current.fingerprint, m_current.unsaved));
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        { m_issues.Add(new(m_targets[completedChapters], error.Message)); FinishChapter(); }
    }

    /// <summary>Stops future work and releases the current graph enumerator while retaining already visible results.</summary>
    public void Cancel() { if (completed) return; cancelled = completed = true; Release(); }
    /// <summary>Releases the owner-local search without saving or closing any document.</summary>
    public void Dispose() { if (m_disposed) return; Cancel(); Release(); m_disposed = true; }
    private void FinishChapter() { Release(); completedChapters++; completed = completedChapters >= m_targets.Length; }
    private void Release() { m_cursor?.Dispose(); m_cursor = null; m_current = null; }
    private static IEnumerable<AssetPath> Paths(NovelProjectAsset project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.chapters.Select(static chapter => chapter?.assetPath ?? default);
    }
}

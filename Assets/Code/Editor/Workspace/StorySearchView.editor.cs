using System;
using System.Collections.Generic;
using InnoEngine.Graphs;
using InnoEngine.Assets;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Editor.Localization;

namespace Inno.VisualNovel.Editor.Workspace;

internal sealed class StorySearchView()
{
    private string m_query = string.Empty;
    private bool m_bookmarks;
    private Guid m_document;
    private int m_page;
    private IReadOnlyList<StorySearchResult> m_results = [];
    private bool m_gameMode;
    private AssetPath? m_game;
    private string m_gameTitle = string.Empty, m_message = string.Empty;
    private GameStorySearch? m_job;
    internal void Draw(
        StoryDocumentsModule documents,
        StoryDraftState draft,
        Action<string> select,
        EditorInteractions interactions
    ) {
        bool changed = Query();
        if (ImGui.Checkbox("Search all chapters in selected game", ref m_gameMode)) { Stop(); m_page = 0; changed = true; }
        if (m_gameMode) { DrawGame(documents, select, interactions); return; }
        changed |= ImGui.Button("Refresh current chapter search");
        if (m_document != documents.activeDocumentId) { m_document = documents.activeDocumentId; changed = true; }
        if (changed) { m_results = documents.search.Find(draft, m_query, m_bookmarks); m_page = 0; }
        EditorUiSession.current.widgets.Hint("Results are a chapter snapshot. Refresh after edits, undo or redo; navigation uses shared node identity.");
        int pages = Math.Max(1, (m_results.Count + 39) / 40); m_page = Math.Clamp(m_page, 0, pages - 1);
        ImGui.Text(m_results.Count + " results · " + (m_page + 1) + " / " + pages);
        if (ImGui.Button("Previous page")) m_page = Math.Max(0, m_page - 1); ImGui.SameLine(); if (ImGui.Button("Next page")) m_page = Math.Min(pages - 1, m_page + 1);
        for (int index = m_page * 40; index < Math.Min(m_results.Count, (m_page + 1) * 40); index++)
        {
            var result = m_results[index]; bool exists = draft.graph.document.FindNode(new(result.nodeId)) is not null;
            ImGui.BeginDisabled(!exists);
            try { if (ImGui.Selectable((result.bookmarked ? "★ " : "") + result.title + " · " + result.nodeId + "##search-" + result.nodeId)) select(result.nodeId); }
            finally { ImGui.EndDisabled(); }
            EditorUiSession.current.widgets.Hint(exists ? result.excerpt : "This node was deleted; refresh search to update results.");
        }
    }

    internal void Stop() { m_job?.Dispose(); m_job = null; }

    internal void DrawGameOnly(
        StoryDocumentsModule documents,
        EditorInteractions interactions
    ) {
        Query(); DrawGame(documents, node => StoryNodeSelection.Select(interactions, documents.activeDocumentId, node), interactions);
    }

    private bool Query()
    {
        bool changed = ImGui.InputText("Search text, names, IDs, resources, tags and notes", ref m_query, 1024);
        return ImGui.Checkbox("Bookmarks only", ref m_bookmarks) || changed;
    }

    private void DrawGame(
        StoryDocumentsModule documents,
        Action<string> select,
        EditorInteractions interactions
    ) {
        if (ImGui.Button("Search selected game"))
        {
            var game = NovelResourceSelection.Get<NovelProjectAsset>(interactions) ?? throw new InvalidOperationException("Select an .ivnproject game definition in File Browser.");
            m_game = game.assetPath; Begin(game);
        }
        ImGui.SameLine(); ImGui.BeginDisabled(m_game is null);
        try { if (ImGui.Button("Search this game again")) Begin(Assets.Load<NovelProjectAsset>(m_game!.Value)); }
        finally { ImGui.EndDisabled(); }
        if (m_job is null)
        {
            EditorUiSession.current.widgets.Hint("Select a game definition to search. Search does not open or save chapters in bulk; open documents use their current unsaved drafts."); return;
        }
        if (!m_job.completed)
        {
            ImGui.SameLine(); if (ImGui.Button("Cancel search")) m_job.Cancel();
            if (!m_job.completed) m_job.Step(128);
        }
        ImGui.Text(m_gameTitle + " · " + m_job.completedChapters + " / " + m_job.chapterCount + " chapters · " + m_job.processedNodes + " nodes"
            + (m_job.cancelled ? " · Cancelled (partial results retained)" : m_job.completed ? " · Search finished" : " · Searching"));
        if (m_job.query != m_query || m_job.bookmarksOnly != m_bookmarks) EditorUiSession.current.widgets.Hint("The query changed; search again to update game-wide results.");
        if (m_job.truncated) EditorUiSession.current.widgets.Hint("The 100000-result limit was reached; use a more specific query.");
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
        foreach (var issue in m_job.issues) EditorUiSession.current.widgets.Hint(issue.path + " · " + issue.message);
        int pages = Math.Max(1, (m_job.results.Count + 39) / 40); m_page = Math.Clamp(m_page, 0, pages - 1);
        ImGui.Text(m_job.results.Count + " results · " + (m_page + 1) + " / " + pages);
        if (ImGui.Button("Previous##game-search")) m_page = Math.Max(0, m_page - 1);
        ImGui.SameLine(); if (ImGui.Button("Next##game-search")) m_page = Math.Min(pages - 1, m_page + 1);
        for (int index = m_page * 40; index < Math.Min(m_job.results.Count, (m_page + 1) * 40); index++)
        {
            var result = m_job.results[index];
            if (ImGui.Selectable(result.chapterTitle + (result.unsaved ? " · Unsaved" : "") + " · " + result.match.title + "##game-search-" + index))
            {
                var current = documents.Inspect(result.path);
                if (current.draft.graph.document.FindNode(new(result.match.nodeId)) is null) m_message = "This node was deleted; search again.";
                else
                {
                    documents.Open(result.path); select(result.match.nodeId);
                    m_message = current.fingerprint == result.fingerprint ? "Located native node." : "Content changed; located by stable node identity. Search again to update summaries.";
                }
            }
            EditorUiSession.current.widgets.Hint(result.path + " · " + result.match.excerpt);
        }

        void Begin(NovelProjectAsset game)
        {
            if (game.isMissing) throw new InvalidOperationException("The selected game definition is missing.");
            Stop(); m_job = new(game, documents.search, documents.Inspect, m_query, m_bookmarks); m_gameTitle = game.title; m_message = string.Empty; m_page = 0;
        }
    }
}

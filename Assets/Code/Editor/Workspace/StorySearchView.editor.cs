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

internal sealed class StorySearchView(EditorTextView text)
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
        if (ImGui.Checkbox(text.Label("搜索所选游戏全部章节"), ref m_gameMode)) { Stop(); m_page = 0; changed = true; }
        if (m_gameMode) { DrawGame(documents, select, interactions); return; }
        changed |= ImGui.Button(text.Label("刷新当前章节搜索"));
        if (m_document != documents.activeDocumentId) { m_document = documents.activeDocumentId; changed = true; }
        if (changed) { m_results = documents.search.Find(draft, m_query, m_bookmarks); m_page = 0; }
        EditorUiSession.current.widgets.Hint(text.Localize("搜索结果是当前章节快照。修改内容、撤销或重做后，点击刷新更新；定位使用共享节点身份。"));
        int pages = Math.Max(1, (m_results.Count + 39) / 40); m_page = Math.Clamp(m_page, 0, pages - 1);
        ImGui.Text(m_results.Count + text.Localize(" 个结果 · ") + (m_page + 1) + " / " + pages);
        if (ImGui.Button(text.Label("上一页"))) m_page = Math.Max(0, m_page - 1); ImGui.SameLine(); if (ImGui.Button(text.Label("下一页"))) m_page = Math.Min(pages - 1, m_page + 1);
        for (int index = m_page * 40; index < Math.Min(m_results.Count, (m_page + 1) * 40); index++)
        {
            var result = m_results[index]; bool exists = draft.graph.document.FindNode(new(result.nodeId)) is not null;
            ImGui.BeginDisabled(!exists);
            try { if (ImGui.Selectable((result.bookmarked ? "★ " : "") + result.title + " · " + result.nodeId + "##search-" + result.nodeId)) select(result.nodeId); }
            finally { ImGui.EndDisabled(); }
            EditorUiSession.current.widgets.Hint(exists ? result.excerpt : text.Localize("该节点已删除；刷新搜索以更新结果。"));
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
        bool changed = ImGui.InputText(text.Label("搜索正文、姓名、ID、资源、标签与备注"), ref m_query, 1024);
        return ImGui.Checkbox(text.Label("只看书签"), ref m_bookmarks) || changed;
    }

    private void DrawGame(
        StoryDocumentsModule documents,
        Action<string> select,
        EditorInteractions interactions
    ) {
        if (ImGui.Button(text.Label("使用所选游戏开始搜索")))
        {
            var game = NovelResourceSelection.Get<NovelProjectAsset>(interactions) ?? throw new InvalidOperationException(text.Localize("请在 File Browser 选择 .ivnproject 游戏定义。"));
            m_game = game.assetPath; Begin(game);
        }
        ImGui.SameLine(); ImGui.BeginDisabled(m_game is null);
        try { if (ImGui.Button(text.Label("重新搜索此游戏"))) Begin(Assets.Load<NovelProjectAsset>(m_game!.Value)); }
        finally { ImGui.EndDisabled(); }
        if (m_job is null)
        {
            EditorUiSession.current.widgets.Hint(text.Localize("选择游戏定义，再开始搜索。搜索不会批量打开或保存章节；已打开文档使用当前未保存草稿。")); return;
        }
        if (!m_job.completed)
        {
            ImGui.SameLine(); if (ImGui.Button(text.Label("取消搜索"))) m_job.Cancel();
            if (!m_job.completed) m_job.Step(128);
        }
        ImGui.Text(m_gameTitle + " · " + m_job.completedChapters + " / " + m_job.chapterCount + text.Localize(" 章 · ") + m_job.processedNodes + text.Localize(" 个节点")
            + (m_job.cancelled ? text.Localize(" · 已取消（保留部分结果）") : m_job.completed ? text.Localize(" · 搜索结束") : text.Localize(" · 搜索中")));
        if (m_job.query != m_query || m_job.bookmarksOnly != m_bookmarks) EditorUiSession.current.widgets.Hint(text.Localize("查询已修改；点击重新搜索更新全游戏结果。"));
        if (m_job.truncated) EditorUiSession.current.widgets.Hint(text.Localize("结果达到 100000 项上限；请使用更精确的查询。"));
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
        foreach (var issue in m_job.issues) EditorUiSession.current.widgets.Hint(issue.path + " · " + issue.message);
        int pages = Math.Max(1, (m_job.results.Count + 39) / 40); m_page = Math.Clamp(m_page, 0, pages - 1);
        ImGui.Text(m_job.results.Count + text.Localize(" 个结果 · ") + (m_page + 1) + " / " + pages);
        if (ImGui.Button(text.Label("上一页##game-search"))) m_page = Math.Max(0, m_page - 1);
        ImGui.SameLine(); if (ImGui.Button(text.Label("下一页##game-search"))) m_page = Math.Min(pages - 1, m_page + 1);
        for (int index = m_page * 40; index < Math.Min(m_job.results.Count, (m_page + 1) * 40); index++)
        {
            var result = m_job.results[index];
            if (ImGui.Selectable(text.Caption(result.chapterTitle + (result.unsaved ? " · 未保存" : "") + " · " + result.match.title + "##game-search-" + index, result.chapterTitle + (result.unsaved ? text.Localize(" · 未保存") : "") + " · " + result.match.title + "##game-search-" + index)))
            {
                var current = documents.Inspect(result.path);
                if (current.draft.graph.document.FindNode(new(result.match.nodeId)) is null) m_message = text.Localize("此节点已删除；请重新搜索。");
                else
                {
                    documents.Open(result.path); select(result.match.nodeId);
                    m_message = current.fingerprint == result.fingerprint ? text.Localize("已定位原生节点。") : text.Localize("内容已变化，已按稳定节点身份定位；请重新搜索更新摘要。");
                }
            }
            EditorUiSession.current.widgets.Hint(result.path + " · " + result.match.excerpt);
        }

        void Begin(NovelProjectAsset game)
        {
            if (game.isMissing) throw new InvalidOperationException(text.Localize("所选游戏定义缺失。"));
            Stop(); m_job = new(game, documents.search, documents.Inspect, m_query, m_bookmarks); m_gameTitle = game.title; m_message = string.Empty; m_page = 0;
        }
    }
}

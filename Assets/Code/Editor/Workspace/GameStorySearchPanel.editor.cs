using System;
using System.Numerics;
using InnoEditor.Core;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Editor.Localization;

namespace Inno.VisualNovel.Editor.Workspace;

/// <summary>Searches an explicit game's authored chapters without requiring an already open story document.</summary>
[EditorPanel("inno.vn.game-search", "VN 全游戏搜索 · Game Search", order: 461, menuPath: "Authoring")]
public sealed class GameStorySearchPanel : EditorPanel
{
    private readonly StoryDocumentsModule m_documents;
    private readonly EditorInteractions m_interactions;
    private readonly EditorTextView m_text;
    private readonly StorySearchView m_view;
    private string m_error = string.Empty;
    /// <summary>Uses the same document snapshots and primary node selection as the VN workspace.</summary>
    /// <param name="documents">The current generation's authored graph owner.</param>
    /// <param name="interactions">The engine's asset selection and document coordinator.</param>
    public GameStorySearchPanel(
        StoryDocumentsModule documents,
        EditorInteractions interactions
    ) { m_documents = documents; m_interactions = interactions; m_text = new(interactions); m_view = new(m_text); }
    /// <inheritdoc />
    public override Vector2 initialSize => new(880, 720);
    /// <inheritdoc />
    protected override void OnDraw(EditorContext context)
    {
        try { m_view.DrawGameOnly(m_documents, m_interactions); }
        catch (Exception error) when (error is not OutOfMemoryException) { m_error = m_text.Error(error); }
        if (m_error.Length > 0)
        { EditorUiSession.current.widgets.Hint(m_error); if (ImGui.SmallButton(m_text.Label("清除搜索提示"))) m_error = string.Empty; }
    }
    /// <inheritdoc />
    protected override void OnDetach(EditorContext context) => m_view.Stop();
}

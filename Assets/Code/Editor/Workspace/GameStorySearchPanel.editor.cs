using System;
using System.Numerics;
using InnoEditor.Core;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Editor.Localization;

namespace Inno.VisualNovel.Editor.Workspace;

/// <summary>Searches an explicit game's authored chapters without requiring an already open story document.</summary>
[EditorPanel("inno.vn.game-search", "VN Game Search", order: 461, defaultOpen: false, menuPath: "Authoring")]
public sealed class GameStorySearchPanel : EditorPanel
{
    private readonly StoryDocumentsModule m_documents;
    private readonly EditorInteractions m_interactions;
    private readonly StorySearchView m_view;
    private string m_error = string.Empty;
    /// <summary>Uses the same document snapshots and primary node selection as the VN workspace.</summary>
    /// <param name="documents">The current generation's authored graph owner.</param>
    /// <param name="interactions">The engine's asset selection and document coordinator.</param>
    public GameStorySearchPanel(
        StoryDocumentsModule documents,
        EditorInteractions interactions
    ) { m_documents = documents; m_interactions = interactions;  m_view = new(); }
    /// <inheritdoc />
    public override Vector2 initialSize => new(880, 720);
    /// <inheritdoc />
    protected override void OnDraw(EditorContext context)
    {
        try { m_view.DrawGameOnly(m_documents, m_interactions); }
        catch (Exception error) when (error is not OutOfMemoryException) { m_error = (error).Message; }
        if (m_error.Length > 0)
        { EditorUiSession.current.widgets.Hint(m_error); if (ImGui.SmallButton("Clear search message")) m_error = string.Empty; }
    }
    /// <inheritdoc />
    protected override void OnDetach(EditorContext context) => m_view.Stop();
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using InnoEngine.Assets;
using InnoEditor.Core;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Variables;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Editor.Variables;

/// <summary>Edits native shared declarations and locates references inside an explicitly selected game's chapters.</summary>
[EditorPanel("inno.vn.shared-variables", "VN Shared Variables", order: 457, defaultOpen: false, menuPath: "Authoring")]
public sealed class SharedVariablesPanel : EditorPanel, IEditorPanelReloadState
{
    private readonly ResourceDocumentsModule m_resources;
    private readonly StoryDocumentsModule m_stories;
    private readonly EditorInteractions m_interactions;
    private readonly StoryInspectorFields m_fields;
    private readonly VariableDefinitionFields m_definition;
    private readonly VariableCatalogView m_catalog;
    private readonly ResourceDocumentToolbar m_toolbar;
    private readonly List<(string chapter, AssetPath path, string node)> m_usages = [];
    private NovelProjectAsset? m_context;
    private string m_selected = string.Empty, m_newId = "variable", m_message = string.Empty;
    private Guid m_document;
    private bool m_confirmRemoval;

    /// <summary>Binds shared native documents, chapter inspection and the existing editor History owner.</summary>
    /// <param name="resources">The resource document service.</param>
    /// <param name="stories">The shared graph and script document service.</param>
    /// <param name="interactions">Native selection, documents and History.</param>
    public SharedVariablesPanel(
        ResourceDocumentsModule resources,
        StoryDocumentsModule stories,
        EditorInteractions interactions
    ) {
        m_resources = resources; m_stories = stories; m_interactions = interactions; m_fields = new(interactions);
        m_definition = new(m_fields); m_catalog = new(m_fields); m_toolbar = new(resources, interactions, m_fields.Finish);
    }
    /// <inheritdoc />
    public override Vector2 initialSize => new(900, 720);
    /// <inheritdoc />
    protected override void OnDraw(EditorContext context)
    {
        m_fields.BeginFrame();
        try { Draw(); }
        catch (Exception failure) when (failure is not OutOfMemoryException) { m_message = (failure).Message; }
        finally { m_fields.EndFrame(); }
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
    }
    /// <inheritdoc />
    public ReadOnlyMemory<byte> CaptureReloadState() { m_fields.Finish(); return m_resources.CaptureReload(); }
    /// <inheritdoc />
    public void RestoreReloadState(ReadOnlyMemory<byte> state) => m_resources.RestoreReload(state.Span);
    /// <inheritdoc />
    protected override void OnDetach(EditorContext context) => m_fields.Finish();

    private void Draw()
    {
        if (!m_toolbar.Draw("Shared variable library", ".ivnvariables", static () => new VariableTableAsset(), out VariableTableAsset? table) || table is null) return;
        if (m_document != m_toolbar.documentId) { m_fields.Finish(); m_document = m_toolbar.documentId; m_selected = string.Empty; m_confirmRemoval = false; m_usages.Clear(); }
        string title = table.title;
        if (m_fields.Text("table-title", "Variable library name", ref title)) m_resources.Edit<VariableTableAsset>(m_document, "Rename shared variable library", candidate => candidate.title = title, m_fields.mergeKey);
        EditorUiSession.current.widgets.Hint("Library declarations compile when a referencing chapter is imported. Save the edited library to update parameter pickers. Unsaved library drafts participate in production-chain preview. Local variables remain in chapters; existing game sessions retain their frozen program.");
        m_fields.Text("shared-new-id", "New variable stable ID", ref m_newId);
        if (ImGui.Button("Add shared variable"))
        {
            m_fields.Finish(); m_resources.Edit<VariableTableAsset>(m_document, "Add shared variable", candidate =>
            {
                if (candidate.variables.Any(item => item.id == m_newId)) throw new InvalidOperationException("Variable ID already exists: " + m_newId);
                VariableTableEditService.Set(candidate, new() { id = m_newId, name = m_newId, scope = VariableScope.Game, initialValue = NovelValue.Boolean(false) });
            });
            m_selected = m_newId; m_usages.Clear(); return;
        }
        string chosen = m_catalog.Draw(table.variables, m_selected);
        if (chosen != m_selected) { m_selected = chosen; m_confirmRemoval = false; m_usages.Clear(); }
        var selected = table.variables.SingleOrDefault(item => item.id == m_selected);
        if (selected is null) return;
        if (m_definition.Draw(selected, true)) m_resources.Edit<VariableTableAsset>(m_document, "Edit shared variable", candidate => VariableTableEditService.Set(candidate, selected), m_fields.mergeKey);
        if (NovelResourceSelection.Get<NovelProjectAsset>(m_interactions) is { } game && ImGui.Button("Find references in selected game")) { m_fields.Finish(); m_context = game; m_usages.Clear(); }
        if (m_context is not null)
        {
            ImGui.Text("Reference scope · " + m_context.title);
            if (ImGui.Button("Find current variable references"))
            {
                m_fields.Finish(); m_usages.Clear();
                var document = m_interactions.documents.documents.Single(item => item.documentId == m_document);
                Guid id = Assets.Load<VariableTableAsset>(AssetPath.Parse(document.assetPath)).identity.persistentId;
                var references = new VariableEditService(m_stories.codec);
                foreach (var chapter in m_context.chapters)
                {
                    var source = m_stories.Inspect(chapter.assetPath).draft;
                    if (source.variableTableId != id) continue;
                    foreach (string node in references.FindUsages(source, selected.id)) m_usages.Add((source.chapterId, chapter.assetPath, node));
                }
                m_message = "Searched the selected game. These node-reference results exclude other games and UI/collection expressions.";
            }
            foreach (var usage in m_usages)
                if (ImGui.Selectable(usage.chapter + "/" + usage.node))
                {
                    m_fields.Finish(); var document = m_stories.Open(usage.path);
                    if (m_stories.Read(document.documentId).graph.document.FindNode(new(usage.node)) is null) throw new InvalidOperationException("Reference results are stale. Search again.");
                    StoryNodeSelection.Select(m_interactions, document.documentId, usage.node);
                }
        }
        EditorUiSession.current.widgets.Hint("Deleting or changing a type may affect other chapters, UI and collection conditions. Diagnostics do not rewrite the story. Update references before saving; library deletion requires an explicit choice.");
        m_fields.Toggle("shared-confirm-removal", "I reviewed references and confirm deleting this declaration", ref m_confirmRemoval);
        ImGui.BeginDisabled(!m_confirmRemoval || m_usages.Count > 0);
        try
        {
            if (ImGui.Button("Delete shared declaration"))
            { m_fields.Finish(); m_resources.Edit<VariableTableAsset>(m_document, "Delete shared variable", candidate => VariableTableEditService.Remove(candidate, selected.id)); m_selected = string.Empty; m_usages.Clear(); m_confirmRemoval = false; }
        }
        finally { ImGui.EndDisabled(); }
    }
}

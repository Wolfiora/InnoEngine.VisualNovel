using System;
using System.Linq;
using System.Numerics;
using InnoEngine.Assets;
using InnoEngine.Rendering;
using InnoEngine.Text;
using InnoEditor.Assets;
using InnoEditor.Core;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using InnoEditor.Rendering;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Integration.Rendering;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.Authoring.ProjectEditing;
using Inno.VisualNovel.UI.Documents;
using Inno.VisualNovel.Model.Interface.Animation;

namespace Inno.VisualNovel.Editor.ScreenDesigner;

/// <summary>Edits the same native UI declarations consumed by the production game runtime.</summary>
[EditorPanel("inno.vn.screen-designer", "VN Screen Designer", order: 451, defaultOpen: false, menuPath: "Authoring")]
public sealed class ScreenDesignerPanel : EditorPanel, IEditorPanelReloadState
{
    private readonly ResourceDocumentsModule m_documents;
    private readonly EditorInteractions m_interactions;
    private readonly StoryInspectorFields m_fields;
    private readonly ScreenElementInspector m_inspector;
    private readonly ScreenTextResourcesView m_textResources;
    private readonly ScreenSettingsView m_settings;
    private readonly ScreenStateView m_state;
    private readonly UiHierarchyView m_hierarchy;
    private readonly UiTreeDragView m_treeDrag;
    private readonly NovelPreviewCanvas m_preview;
    private readonly ScreenPreviewDataSource m_data = new();
    private string m_selected = string.Empty;
    private string m_message = string.Empty;
    private UiElementKind m_addKind = UiElementKind.Button;
    private Guid m_document;
    private bool m_treeChanged;
    private bool m_animationPreview;
    private bool m_previewReducedMotion;
    private bool m_previewHighContrast;
    private UiAnimationEvent m_animationEvent;
    private double m_animationTime;
    private readonly ScreenCanvasGestures m_gestures;
    private bool m_canvasDrawn;
    private VariableDefinition[] m_variables = [];
    private Model.Interface.Actions.UiActionDefinition[] m_actions = [];
    private Model.Interface.Data.UiDataSourceDefinition[] m_sources = [];
    private ThemeAsset? m_theme;

    /// <summary>Binds resource documents, engine History and the shared native preview bridge.</summary>
    /// <param name="documents">The resource document owner.</param>
    /// <param name="interactions">The host selection and History coordinator.</param>
    /// <param name="previews">The host-owned offscreen preview service.</param>
    public ScreenDesignerPanel(
        ResourceDocumentsModule documents,
        EditorInteractions interactions,
        IEditorPreviewService previews
    ) {
        m_documents = documents; m_interactions = interactions;
        m_fields = new(interactions); m_treeDrag = new(); m_inspector = new(m_fields); m_textResources = new(m_fields); m_settings = new(m_fields); m_state = new(m_fields); m_hierarchy = new(m_fields); m_preview = new(previews);
        m_gestures = new(documents, interactions, m_fields);
    }
    /// <inheritdoc />
    public override Vector2 initialSize => new(1320, 800);
    /// <inheritdoc />
    public override bool allowScrolling => false;
    /// <inheritdoc />
    protected override void OnDraw(EditorContext context)
    {
        m_fields.BeginFrame();
        m_canvasDrawn = false;
        try { Draw(); }
        catch (Exception error) when (error is not OutOfMemoryException) { m_message = (error).Message; }
        finally { if (!m_canvasDrawn) m_gestures.Finish(); m_fields.EndFrame(); }
    }
    /// <inheritdoc />
    public ReadOnlyMemory<byte> CaptureReloadState() { Finish(); return m_documents.CaptureReload(); }
    /// <inheritdoc />
    public void RestoreReloadState(ReadOnlyMemory<byte> state) => m_documents.RestoreReload(state.Span);
    /// <inheritdoc />
    protected override void OnDetach(EditorContext context) { Finish(); m_preview.Dispose(); }

    private void Draw()
    {
        if (ImGui.Button("Create UI screen"))
        {
            Finish();
            m_documents.Create(new ScreenAsset { title = "New screen", font = BuiltInNovelAssets.Load<FontAsset>("BuiltIn/Fonts/NotoSansCJKsc-Regular.otf"),
                root = new() { id = "root", kind = UiElementKind.Panel, layout = new() { width = 1920, height = 1080 } } });
        }
        ImGui.SameLine();
        if (ImGui.Button("Open selected screen"))
        {
            Finish();
            AssetPath? path = m_interactions.selection.selectedTarget switch { ScreenAsset asset => asset.assetPath, AssetFileEntry file when file.extension == ".ivnui" => file.assetPath, _ => null };
            if (path is null) m_message = "Select an .ivnui screen in File Browser."; else m_documents.Open(path.Value);
        }
        var document = m_documents.activeDocument;
        if (document is null || !document.assetPath.EndsWith(".ivnui", StringComparison.OrdinalIgnoreCase))
        {
            EditorUiSession.current.widgets.Hint("Create a screen or open one generated by the game wizard. Its element tree, canvas and properties share one document and Undo/Redo history.");
            return;
        }
        ImGui.SameLine(); if (ImGui.Button("Save")) { Finish(); m_interactions.documents.Save(document.documentId); }
        ImGui.SameLine(); if (ImGui.Button("Undo")) { Finish(); m_interactions.history.Undo(); }
        ImGui.SameLine(); if (ImGui.Button("Redo")) { Finish(); m_interactions.history.Redo(); }
        ImGui.Text(document.title + (document.isDirty ? " · Unsaved" : " · Saved"));
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
        if (m_document != document.documentId) { Finish(); m_document = document.documentId; m_selected = string.Empty; m_hierarchy.Reset(); }
        ScreenAsset screen = m_documents.Read<ScreenAsset>(m_document);
        if (NovelResourceSelection.Get<ThemeAsset>(m_interactions) is { } theme && ImGui.Button("Preview and choose styles with selected theme")) { Finish(); m_theme = theme; }
        if (m_theme is not null)
        { ImGui.Text("Preview theme: " + m_theme.title); ImGui.SameLine(); if (ImGui.SmallButton("View screen's own styling")) { Finish(); m_theme = null; } }
        m_selected = ScreenElementSelection.Resolve(m_interactions, m_document, screen, m_selected);
        Vector2 available = ImGui.GetContentRegionAvailable();
        m_treeChanged = false;
        Child("page-tree", new(220, 0), () => Tree(screen));
        if (m_treeChanged) { screen = m_documents.Read<ScreenAsset>(m_document); m_selected = ScreenElementSelection.Resolve(m_interactions, m_document, screen, m_selected); }
        ImGui.SameLine();
        Child("page-canvas", new(Math.Max(200, available.X - 600), 0), () => Canvas(screen));
        ImGui.SameLine();
        Child("page-properties", new(0, 0), () => Properties(screen));
    }
    private void Tree(ScreenAsset screen)
    {
        ImGui.Text("Screen elements");
        var rows = ScreenEditService.Flatten(screen); long revision = m_documents.GetRevision(m_document);
        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            if (ImGui.Selectable(new string(' ', row.depth * 2) + row.element.kind + " · " + row.element.id + "##" + row.element.id, m_selected == row.element.id))
            { Finish(); SelectElement(row.element.id); }
            if (m_treeDrag.DrawRow(m_document, revision, screen, rows, rowIndex) is { } destination)
            {
                Finish(); SelectElement(destination.sourceId);
                if (destination.changesParent) m_hierarchy.Propose(destination);
                else
                {
                    m_documents.Edit<ScreenAsset>(m_document, "Drag to reorder UI", candidate => ScreenEditService.Reparent(candidate, destination.sourceId, destination.parentId, destination.index));
                    m_treeChanged = true; return;
                }
            }
        }
        m_fields.SelectEnum("add-kind", "New control", ref m_addKind);
        if (ImGui.Button("Add to selected element"))
        {
            Finish(); string selected = m_selected;
            string created = string.Empty;
            m_documents.Edit<ScreenAsset>(m_document, "Add UI control", candidate => created = ScreenEditService.Add(candidate, selected, m_addKind)); SelectElement(created); m_treeChanged = true; return;
        }
        if (ImGui.Button("Save selected subtree as a component template"))
        {
            Finish(); var template = UiWidgetEditService.Extract(screen, m_selected, "Component · " + m_selected);
            var created = m_documents.Create(template); m_message = "Created editable component template: " + created.assetPath;
        }
        if (NovelResourceSelection.Get<ScreenAsset>(m_interactions) is { } templateSource && ImGui.Button("Insert selected screen / component template"))
        {
            Finish(); string parent = m_selected;
            string created = string.Empty;
            m_documents.Edit<ScreenAsset>(m_document, "Instantiate UI component", candidate => created = UiWidgetEditService.Insert(candidate, parent, templateSource)); SelectElement(created); m_treeChanged = true; return;
        }
        if (m_selected == screen.root.id) return;
        if (m_hierarchy.Draw(screen, m_selected, out string parentId, out int index))
        {
            Finish(); string selected = m_selected;
            m_documents.Edit<ScreenAsset>(m_document, "Change UI hierarchy and order", candidate => ScreenEditService.Reparent(candidate, selected, parentId, index));
            m_treeChanged = true; return;
        }
        if (ImGui.Button("Duplicate selected subtree"))
        {
            Finish(); string selected = m_selected;
            string created = string.Empty;
            m_documents.Edit<ScreenAsset>(m_document, "Duplicate UI subtree", candidate => created = ScreenEditService.Duplicate(candidate, selected)); SelectElement(created); m_treeChanged = true; return;
        }
        if (ImGui.Button("Delete selected subtree"))
        {
            Finish(); string selected = m_selected;
            m_documents.Edit<ScreenAsset>(m_document, "Delete UI subtree", candidate => ScreenEditService.Remove(candidate, selected)); SelectElement(screen.root.id); m_treeChanged = true; return;
        }
    }
    private void Properties(ScreenAsset screen)
    {
        if (m_state.Draw(screen)) m_documents.Edit<ScreenAsset>(m_document, "Edit screen-local state", candidate =>
        { candidate.stateVariables = screen.stateVariables; candidate.root = screen.root; }, m_fields.mergeKey);
        if (m_settings.Draw(screen, m_interactions)) m_documents.Edit<ScreenAsset>(m_document, "Edit screen settings", candidate =>
        { candidate.title = screen.title; candidate.width = screen.width; candidate.height = screen.height; candidate.modal = screen.modal; candidate.initialFocus = screen.initialFocus; candidate.font = screen.font; }, m_fields.mergeKey);
        if (m_textResources.Draw(screen, m_interactions)) m_documents.Edit<ScreenAsset>(m_document, "Edit screen rich-text resources", candidate =>
        { candidate.textFonts = screen.textFonts; candidate.textIcons = screen.textIcons; }, m_fields.mergeKey);
        UiElementDefinition element = ScreenEditService.Find(screen, m_selected);
        if (NovelResourceSelection.Get<NovelProjectAsset>(m_interactions) is { } game && ImGui.Button("Use variables, actions and data sources from selected game"))
        { Finish(); m_variables = NovelProjectVariables.Read(game); m_actions = game.uiActions.Select(Model.Interface.Actions.UiActionContract.Copy).ToArray(); m_sources = game.uiDataSources.Select(Model.Interface.Data.UiDataSourceContract.Copy).ToArray(); }
        ImGui.Text("Control properties · " + element.kind);
        if (m_inspector.Draw(element, m_variables, m_theme, screen.stateVariables, Inno.VisualNovel.UI.Input.UiFocusTargets.Enumerate(screen.root).Where(static item => item.kind == UiElementKind.ImageViewport).ToArray(), m_actions, m_sources,
            UiDataBindingValidator.RowOwner(screen, element.id, m_sources))) m_documents.Edit<ScreenAsset>(m_document, "Edit UI control", candidate =>
        {
            if (candidate.root.id == element.id) candidate.root = element;
            else
            {
                var row = ScreenEditService.Flatten(candidate).Single(item => item.element.id == element.id);
                row.parent!.children[Array.FindIndex(row.parent.children, child => child.id == element.id)] = element;
            }
        }, m_fields.mergeKey);
        if (NovelResourceSelection.Get<TextureAsset>(m_interactions) is { } texture && ImGui.Button("Use selected image"))
        { Finish(); m_documents.Edit<ScreenAsset>(m_document, "Set UI image", candidate => ScreenEditService.Find(candidate, m_selected).image = texture); }
        if (NovelResourceSelection.Get<FontAsset>(m_interactions) is { } font && ImGui.Button("Use selected font"))
        { Finish(); m_documents.Edit<ScreenAsset>(m_document, "Set UI font", candidate => ScreenEditService.Find(candidate, m_selected).font = font); }
    }
    private void Canvas(ScreenAsset screen)
    {
        m_canvasDrawn = true;
        EditorUiSession.current.widgets.Hint("Design mode: drag absolutely positioned controls, or resize Fixed dimensions with edge/corner handles. Arrow keys nudge by 1 px, or 10 px with Shift. Alt temporarily disables snapping; Esc cancels dragging. Preview does not execute game actions.");
        if (ImGui.Checkbox("Animation time preview (without actions)", ref m_animationPreview)) Finish();
        ImGui.Checkbox("Preview high-contrast reading", ref m_previewHighContrast);
        if (m_animationPreview)
        {
            m_fields.SelectEnum("ui-animation-preview/event", "Event", ref m_animationEvent);
            m_fields.Number("ui-animation-preview/time", "Preview time (seconds)", ref m_animationTime, 0, 120);
            ImGui.Checkbox("Preview reduced motion", ref m_previewReducedMotion);
        }
        var instance = new UiDocumentInstance(screen, NovelPresentationFrame.Measure, NovelPresentationFrame.MeasureRichText, m_theme);
        m_data.SetPage(screen.stateVariables);
        instance.SetReducedMotion(m_previewReducedMotion);
        instance.SetHighContrast(m_previewHighContrast);
        UiFrame frame = m_animationPreview ? instance.PreviewAnimation(m_data, m_selected, m_animationEvent, m_animationTime) : instance.BuildFrame(m_data, false);
        m_gestures.Toolbar(m_document, screen, frame, m_selected, !m_animationPreview);
        Vector2 available = ImGui.GetContentRegionAvailable();
        float scale = Math.Max(0.01f, Math.Min(available.X / screen.width, available.Y / screen.height));
        Vector2 size = new(screen.width * scale, screen.height * scale);
        Vector2 origin = ImGui.GetCursorScreenPosition();
        m_preview.Draw(frame, size);
        m_gestures.Draw(m_document, screen, frame, origin, size, scale, m_selected, SelectElement, !m_animationPreview);
    }
    private void Finish() { m_fields.Finish(); m_gestures.Finish(); }
    private void SelectElement(string id) { m_selected = id; ScreenElementSelection.Select(m_interactions, m_document, id); }
    private static void Child(string id, Vector2 size, Action draw)
    {
        bool visible = ImGui.BeginChild(id, size, ImGuiChildFlags.Borders);
        try { if (visible) draw(); } finally { ImGui.EndChild(); }
    }
}

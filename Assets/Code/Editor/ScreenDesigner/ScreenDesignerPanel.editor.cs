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
[EditorPanel("inno.vn.screen-designer", "VN 界面设计 · Screen Designer", order: 451, menuPath: "Authoring")]
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
        m_fields = new(interactions); m_treeDrag = new(m_fields.text); m_inspector = new(m_fields); m_textResources = new(m_fields); m_settings = new(m_fields); m_state = new(m_fields); m_hierarchy = new(m_fields); m_preview = new(previews);
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
        catch (Exception error) when (error is not OutOfMemoryException) { m_message = m_fields.Error(error); }
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
        if (ImGui.Button(m_fields.Label("创建 UI 页面")))
        {
            Finish();
            m_documents.Create(new ScreenAsset { title = "新页面", font = BuiltInNovelAssets.Load<FontAsset>("BuiltIn/Fonts/NotoSansCJKsc-Regular.otf"),
                root = new() { id = "root", kind = UiElementKind.Panel, layout = new() { width = 1920, height = 1080 } } });
        }
        ImGui.SameLine();
        if (ImGui.Button(m_fields.Label("打开所选页面")))
        {
            Finish();
            AssetPath? path = m_interactions.selection.selectedTarget switch { ScreenAsset asset => asset.assetPath, AssetFileEntry file when file.extension == ".ivnui" => file.assetPath, _ => null };
            if (path is null) m_message = m_fields.Localize("在 File Browser 中选择 .ivnui 页面。"); else m_documents.Open(path.Value);
        }
        var document = m_documents.activeDocument;
        if (document is null || !document.assetPath.EndsWith(".ivnui", StringComparison.OrdinalIgnoreCase))
        {
            EditorUiSession.current.widgets.Hint(m_fields.Localize("创建页面，或打开游戏向导生成的任一页面。页面树、画布和属性共用同一文档与 Undo/Redo。"));
            return;
        }
        ImGui.SameLine(); if (ImGui.Button(m_fields.Label("保存"))) { Finish(); m_interactions.documents.Save(document.documentId); }
        ImGui.SameLine(); if (ImGui.Button(m_fields.Label("撤销"))) { Finish(); m_interactions.history.Undo(); }
        ImGui.SameLine(); if (ImGui.Button(m_fields.Label("重做"))) { Finish(); m_interactions.history.Redo(); }
        ImGui.Text(document.title + (document.isDirty ? m_fields.Localize(" · 未保存") : m_fields.Localize(" · 已保存")));
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
        if (m_document != document.documentId) { Finish(); m_document = document.documentId; m_selected = string.Empty; m_hierarchy.Reset(); }
        ScreenAsset screen = m_documents.Read<ScreenAsset>(m_document);
        if (NovelResourceSelection.Get<ThemeAsset>(m_interactions) is { } theme && ImGui.Button(m_fields.Label("使用所选主题预览与选样式"))) { Finish(); m_theme = theme; }
        if (m_theme is not null)
        { ImGui.Text(m_fields.Localize("预览主题：") + m_theme.title); ImGui.SameLine(); if (ImGui.SmallButton(m_fields.Label("查看页面自身样式"))) { Finish(); m_theme = null; } }
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
        ImGui.Text(m_fields.Localize("页面元素"));
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
                    m_documents.Edit<ScreenAsset>(m_document, m_fields.Localize("拖动调整 UI 顺序"), candidate => ScreenEditService.Reparent(candidate, destination.sourceId, destination.parentId, destination.index));
                    m_treeChanged = true; return;
                }
            }
        }
        m_fields.SelectEnum("add-kind", "新控件", ref m_addKind);
        if (ImGui.Button(m_fields.Label("添加到选中元素")))
        {
            Finish(); string selected = m_selected;
            string created = string.Empty;
            m_documents.Edit<ScreenAsset>(m_document, m_fields.Localize("添加 UI 控件"), candidate => created = ScreenEditService.Add(candidate, selected, m_addKind)); SelectElement(created); m_treeChanged = true; return;
        }
        if (ImGui.Button(m_fields.Label("将选中子树保存为组件模板")))
        {
            Finish(); var template = UiWidgetEditService.Extract(screen, m_selected, "组件 · " + m_selected);
            var created = m_documents.Create(template); m_message = m_fields.Localize("已创建原生可编辑组件模板：") + created.assetPath;
        }
        if (NovelResourceSelection.Get<ScreenAsset>(m_interactions) is { } templateSource && ImGui.Button(m_fields.Label("插入所选页面 / 组件模板")))
        {
            Finish(); string parent = m_selected;
            string created = string.Empty;
            m_documents.Edit<ScreenAsset>(m_document, m_fields.Localize("实例化 UI 组件"), candidate => created = UiWidgetEditService.Insert(candidate, parent, templateSource)); SelectElement(created); m_treeChanged = true; return;
        }
        if (m_selected == screen.root.id) return;
        if (m_hierarchy.Draw(screen, m_selected, out string parentId, out int index))
        {
            Finish(); string selected = m_selected;
            m_documents.Edit<ScreenAsset>(m_document, m_fields.Localize("调整 UI 层级与顺序"), candidate => ScreenEditService.Reparent(candidate, selected, parentId, index));
            m_treeChanged = true; return;
        }
        if (ImGui.Button(m_fields.Label("复制选中子树")))
        {
            Finish(); string selected = m_selected;
            string created = string.Empty;
            m_documents.Edit<ScreenAsset>(m_document, m_fields.Localize("复制 UI 子树"), candidate => created = ScreenEditService.Duplicate(candidate, selected)); SelectElement(created); m_treeChanged = true; return;
        }
        if (ImGui.Button(m_fields.Label("删除选中子树")))
        {
            Finish(); string selected = m_selected;
            m_documents.Edit<ScreenAsset>(m_document, m_fields.Localize("删除 UI 子树"), candidate => ScreenEditService.Remove(candidate, selected)); SelectElement(screen.root.id); m_treeChanged = true; return;
        }
    }
    private void Properties(ScreenAsset screen)
    {
        if (m_state.Draw(screen)) m_documents.Edit<ScreenAsset>(m_document, m_fields.Localize("编辑页面局部状态"), candidate =>
        { candidate.stateVariables = screen.stateVariables; candidate.root = screen.root; }, m_fields.mergeKey);
        if (m_settings.Draw(screen, m_interactions)) m_documents.Edit<ScreenAsset>(m_document, m_fields.Localize("编辑页面设置"), candidate =>
        { candidate.title = screen.title; candidate.width = screen.width; candidate.height = screen.height; candidate.modal = screen.modal; candidate.initialFocus = screen.initialFocus; candidate.font = screen.font; }, m_fields.mergeKey);
        if (m_textResources.Draw(screen, m_interactions)) m_documents.Edit<ScreenAsset>(m_document, m_fields.Localize("编辑页面富文本资源"), candidate =>
        { candidate.textFonts = screen.textFonts; candidate.textIcons = screen.textIcons; }, m_fields.mergeKey);
        UiElementDefinition element = ScreenEditService.Find(screen, m_selected);
        if (NovelResourceSelection.Get<NovelProjectAsset>(m_interactions) is { } game && ImGui.Button(m_fields.Label("使用所选游戏的变量、动作与数据源")))
        { Finish(); m_variables = NovelProjectVariables.Read(game); m_actions = game.uiActions.Select(Model.Interface.Actions.UiActionContract.Copy).ToArray(); m_sources = game.uiDataSources.Select(Model.Interface.Data.UiDataSourceContract.Copy).ToArray(); }
        ImGui.Text(m_fields.Localize("控件属性 · ") + element.kind);
        if (m_inspector.Draw(element, m_variables, m_theme, screen.stateVariables, Inno.VisualNovel.UI.Input.UiFocusTargets.Enumerate(screen.root).Where(static item => item.kind == UiElementKind.ImageViewport).ToArray(), m_actions, m_sources,
            UiDataBindingValidator.RowOwner(screen, element.id, m_sources))) m_documents.Edit<ScreenAsset>(m_document, m_fields.Localize("编辑 UI 控件"), candidate =>
        {
            if (candidate.root.id == element.id) candidate.root = element;
            else
            {
                var row = ScreenEditService.Flatten(candidate).Single(item => item.element.id == element.id);
                row.parent!.children[Array.FindIndex(row.parent.children, child => child.id == element.id)] = element;
            }
        }, m_fields.mergeKey);
        if (NovelResourceSelection.Get<TextureAsset>(m_interactions) is { } texture && ImGui.Button(m_fields.Label("使用所选图像")))
        { Finish(); m_documents.Edit<ScreenAsset>(m_document, m_fields.Localize("设置 UI 图像"), candidate => ScreenEditService.Find(candidate, m_selected).image = texture); }
        if (NovelResourceSelection.Get<FontAsset>(m_interactions) is { } font && ImGui.Button(m_fields.Label("使用所选字体")))
        { Finish(); m_documents.Edit<ScreenAsset>(m_document, m_fields.Localize("设置 UI 字体"), candidate => ScreenEditService.Find(candidate, m_selected).font = font); }
    }
    private void Canvas(ScreenAsset screen)
    {
        m_canvasDrawn = true;
        EditorUiSession.current.widgets.Hint(m_fields.Localize("设计模式：拖动绝对控件，边/角手柄缩放 Fixed 尺寸。方向键微调 1 px，Shift 为 10 px；Alt 暂时关闭吸附，Esc 取消拖动。预览不执行游戏动作。"));
        if (ImGui.Checkbox(m_fields.Label("动画时间预览（不执行动作）"), ref m_animationPreview)) Finish();
        ImGui.Checkbox(m_fields.Label("预览高对比阅读"), ref m_previewHighContrast);
        if (m_animationPreview)
        {
            m_fields.SelectEnum("ui-animation-preview/event", "事件", ref m_animationEvent);
            m_fields.Number("ui-animation-preview/time", m_fields.Localize("预览时间（秒）"), ref m_animationTime, 0, 120);
            ImGui.Checkbox(m_fields.Label("预览减少动态效果"), ref m_previewReducedMotion);
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

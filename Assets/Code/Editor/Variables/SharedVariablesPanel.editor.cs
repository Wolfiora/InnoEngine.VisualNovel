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
[EditorPanel("inno.vn.shared-variables", "VN 共享变量 · Shared Variables", order: 457, menuPath: "Authoring")]
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
        catch (Exception failure) when (failure is not OutOfMemoryException) { m_message = m_fields.Error(failure); }
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
        if (!m_toolbar.Draw("共享变量库", ".ivnvariables", static () => new VariableTableAsset(), out VariableTableAsset? table) || table is null) return;
        if (m_document != m_toolbar.documentId) { m_fields.Finish(); m_document = m_toolbar.documentId; m_selected = string.Empty; m_confirmRemoval = false; m_usages.Clear(); }
        string title = table.title;
        if (m_fields.Text("table-title", m_fields.Localize("变量库名称"), ref title)) m_resources.Edit<VariableTableAsset>(m_document, m_fields.Localize("重命名共享变量库"), candidate => candidate.title = title, m_fields.mergeKey);
        EditorUiSession.current.widgets.Hint(m_fields.Localize("库声明由引用它的章节在导入时编译；修改后保存变量库，再使用更新后的参数选择器。未保存的库草稿会参与生产链预览。Local 变量保留在章节。旧游戏会话仍使用启动时冻结的程序。"));
        m_fields.Text("shared-new-id", m_fields.Localize("新变量稳定 ID"), ref m_newId);
        if (ImGui.Button(m_fields.Label("添加共享变量")))
        {
            m_fields.Finish(); m_resources.Edit<VariableTableAsset>(m_document, m_fields.Localize("添加共享变量"), candidate =>
            {
                if (candidate.variables.Any(item => item.id == m_newId)) throw new InvalidOperationException(m_fields.Localize("变量 ID 已存在：") + m_newId);
                VariableTableEditService.Set(candidate, new() { id = m_newId, name = m_newId, scope = VariableScope.Game, initialValue = NovelValue.Boolean(false) });
            });
            m_selected = m_newId; m_usages.Clear(); return;
        }
        string chosen = m_catalog.Draw(table.variables, m_selected);
        if (chosen != m_selected) { m_selected = chosen; m_confirmRemoval = false; m_usages.Clear(); }
        var selected = table.variables.SingleOrDefault(item => item.id == m_selected);
        if (selected is null) return;
        if (m_definition.Draw(selected, true)) m_resources.Edit<VariableTableAsset>(m_document, m_fields.Localize("编辑共享变量"), candidate => VariableTableEditService.Set(candidate, selected), m_fields.mergeKey);
        if (NovelResourceSelection.Get<NovelProjectAsset>(m_interactions) is { } game && ImGui.Button(m_fields.Label("使用所选游戏查询引用"))) { m_fields.Finish(); m_context = game; m_usages.Clear(); }
        if (m_context is not null)
        {
            ImGui.Text(m_fields.Localize("引用范围 · ") + m_context.title);
            if (ImGui.Button(m_fields.Label("查询当前变量引用")))
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
                m_message = m_fields.Localize("已查询所选游戏；其他游戏与 UI/收藏表达式不在此节点引用结果内。");
            }
            foreach (var usage in m_usages)
                if (ImGui.Selectable(usage.chapter + "/" + usage.node))
                {
                    m_fields.Finish(); var document = m_stories.Open(usage.path);
                    if (m_stories.Read(document.documentId).graph.document.FindNode(new(usage.node)) is null) throw new InvalidOperationException(m_fields.Localize("引用结果已过时，请重新查询。"));
                    StoryNodeSelection.Select(m_interactions, document.documentId, usage.node);
                }
        }
        EditorUiSession.current.widgets.Hint(m_fields.Localize("删除或更改类型可能影响其他章节、UI 与收藏条件；诊断不会自动重写故事。先修改引用，再保存。库删除仅在明确选择后执行。"));
        m_fields.Toggle("shared-confirm-removal", "我已检查使用位置，确认删除此声明", ref m_confirmRemoval);
        ImGui.BeginDisabled(!m_confirmRemoval || m_usages.Count > 0);
        try
        {
            if (ImGui.Button(m_fields.Label("删除共享声明")))
            { m_fields.Finish(); m_resources.Edit<VariableTableAsset>(m_document, m_fields.Localize("删除共享变量"), candidate => VariableTableEditService.Remove(candidate, selected.id)); m_selected = string.Empty; m_usages.Clear(); m_confirmRemoval = false; }
        }
        finally { ImGui.EndDisabled(); }
    }
}

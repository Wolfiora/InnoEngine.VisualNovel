using System;
using System.Linq;
using System.Numerics;
using InnoEditor.Core;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Extensions;
using Inno.VisualNovel.Model.Interface.Data;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Editor.ScreenDesigner;

/// <summary>Edits custom typed binding metadata through the same native document and History owners as pages.</summary>
[EditorPanel("inno.vn.ui-data-catalog", "VN 自定义 UI 数据 · Custom UI Data", order: 466, menuPath: "Authoring")]
public sealed class UiDataSourceCatalogPanel : EditorPanel, IEditorPanelReloadState
{
    private readonly ResourceDocumentsModule m_documents;
    private readonly StoryInspectorFields m_fields;
    private readonly UiDataFieldsView m_fieldView;
    private readonly ResourceDocumentToolbar m_toolbar;
    private string m_error = string.Empty;
    /// <summary>Uses native document transactions and editor gestures for complete source schema editing.</summary>
    /// <param name="documents">The native resource document owner.</param>
    /// <param name="interactions">The current generation's History and selection service.</param>
    public UiDataSourceCatalogPanel(
        ResourceDocumentsModule documents,
        EditorInteractions interactions
    ) { m_documents = documents; m_fields = new(interactions); m_fieldView = new(m_fields); m_toolbar = new(documents, interactions, m_fields.Finish); }
    /// <inheritdoc />
    public override Vector2 initialSize => new(700, 720);
    /// <inheritdoc />
    protected override void OnDraw(EditorContext context)
    {
        m_fields.BeginFrame();
        try { Draw(); }
        catch (Exception error) when (error is not OutOfMemoryException) { m_error = m_fields.Error(error); }
        finally { m_fields.EndFrame(); }
        if (m_error.Length > 0) { EditorUiSession.current.widgets.Hint(m_error); if (ImGui.Button(m_fields.Label("清除提示"))) m_error = string.Empty; }
    }
    /// <inheritdoc />
    public ReadOnlyMemory<byte> CaptureReloadState() { m_fields.Finish(); return m_documents.CaptureReload(); }
    /// <inheritdoc />
    public void RestoreReloadState(ReadOnlyMemory<byte> state) => m_documents.RestoreReload(state.Span);
    /// <inheritdoc />
    protected override void OnDetach(EditorContext context) => m_fields.Finish();
    private void Draw()
    {
        if (!m_toolbar.Draw<UiDataSourceCatalogAsset>("UI 数据目录", ".ivnuidata", static () => new(), out var catalog) || catalog is null) return;
        EditorUiSession.current.widgets.Hint(m_fields.Localize("声明字段/列表的类型和读写权限。关联到游戏后，界面设计器会生成绑定菜单。C# 实现通过现有 UiDataSource 接入；持久状态保存在原生变量或页面字段。"));
        if (ImGui.Button(m_fields.Label("添加数据源")))
        { m_fields.Finish(); m_documents.Edit<UiDataSourceCatalogAsset>(m_toolbar.documentId, m_fields.Localize("添加 UI 数据源"), candidate => UiDataSourceCatalogEditService.Set(candidate, new() { id = "s" + Guid.NewGuid().ToString("N"), title = "数据源" })); }
        foreach (var original in catalog.definitions)
        {
            if (!ImGui.CollapsingHeader(original.title + " · custom." + original.id)) continue;
            var item = UiDataSourceContract.Copy(original); string prefix = "source/" + original.id;
            string id = item.id, title = item.title, help = item.help;
            bool changed = m_fields.Text(prefix + "/id", m_fields.Localize("源 ID（单段）"), ref id); item.id = id;
            changed |= m_fields.Text(prefix + "/title", m_fields.Localize("显示名"), ref title); item.title = title;
            changed |= m_fields.MultilineText(prefix + "/help", m_fields.Localize("用途与状态说明"), ref help); item.help = help;
            bool preview = item.capabilities.HasFlag(CommandCapabilities.Preview), rollback = item.capabilities.HasFlag(CommandCapabilities.Rollback), external = item.capabilities.HasFlag(CommandCapabilities.ExternalEffects);
            changed |= m_fields.Toggle(prefix + "/preview", "允许预览查询", ref preview);
            changed |= m_fields.Toggle(prefix + "/rollback", "写入允许跨越回滚", ref rollback);
            changed |= m_fields.Toggle(prefix + "/external", "依赖外部副作用（预览禁用）", ref external);
            item.capabilities = CommandCapabilities.Save | (preview ? CommandCapabilities.Preview : 0) | (rollback && !external ? CommandCapabilities.Rollback : 0) | (external ? CommandCapabilities.ExternalEffects : 0);
            var rootFields = item.fields;
            if (ImGui.CollapsingHeader(m_fields.Caption("根字段##" + prefix, m_fields.Localize("根字段##") + prefix))) changed |= m_fieldView.Draw(prefix + "/root", ref rootFields); item.fields = rootFields;
            string images = string.Join(", ", item.images);
            if (m_fields.Text(prefix + "/images", m_fields.Localize("图片字段 ID（逗号分隔）"), ref images)) { item.images = images.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries); changed = true; }
            if (ImGui.Button(m_fields.Caption("添加列表##" + prefix, m_fields.Localize("添加列表##") + prefix)))
            { m_fields.Finish(); item.collections = [.. item.collections, new() { id = "rows" + Guid.NewGuid().ToString("N"), title = "列表", fields = [new() { schema = new() { id = "text", label = "文字", defaultValue = NovelValue.String("") } }] }]; changed = true; }
            string remove = string.Empty;
            foreach (var collection in item.collections)
            {
                if (!ImGui.CollapsingHeader(m_fields.Caption("列表 · " + collection.title + "##" + prefix + "/" + collection.id, m_fields.Localize("列表 · ") + collection.title + "##" + prefix + "/" + collection.id))) continue;
                string collectionId = collection.id, collectionTitle = collection.title;
                changed |= m_fields.Text(prefix + "/" + collection.id + "/id", m_fields.Localize("列表 ID"), ref collectionId); collection.id = collectionId;
                changed |= m_fields.Text(prefix + "/" + collection.id + "/title", m_fields.Localize("显示名"), ref collectionTitle); collection.title = collectionTitle;
                var rowFields = collection.fields; changed |= m_fieldView.Draw(prefix + "/rows/" + collection.id, ref rowFields); collection.fields = rowFields;
                if (ImGui.Button(m_fields.Caption("删除列表##" + prefix + "/" + collection.id, m_fields.Localize("删除列表##") + prefix + "/" + collection.id))) { m_fields.Finish(); remove = collection.id; changed = true; }
            }
            if (remove.Length > 0) item.collections = item.collections.Where(row => row.id != remove).ToArray();
            if (changed) m_documents.Edit<UiDataSourceCatalogAsset>(m_toolbar.documentId, m_fields.Localize("编辑 UI 数据契约"), candidate => UiDataSourceCatalogEditService.Set(candidate, item, original.id), m_fields.mergeKey);
            if (ImGui.Button(m_fields.Caption("删除数据源##" + prefix, m_fields.Localize("删除数据源##") + prefix))) { m_fields.Finish(); m_documents.Edit<UiDataSourceCatalogAsset>(m_toolbar.documentId, m_fields.Localize("删除 UI 数据源"), candidate => UiDataSourceCatalogEditService.Remove(candidate, original.id)); }
        }
    }
}

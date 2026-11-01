using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using InnoEngine.Assets;
using InnoEditor.Core;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.AssetOrganization;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Editor.Presentation;

namespace Inno.VisualNovel.Editor.Resources;

/// <summary>Organizes explicitly selected native resources through ordinary annotation documents and engine History.</summary>
[EditorPanel("inno.vn.resource-labels", "VN 资源整理 · Resource Labels", order: 469, menuPath: "Authoring")]
public sealed class ResourceLabelPanel : EditorPanel, IEditorPanelReloadState
{
    private readonly ResourceDocumentsModule m_documents;
    private readonly EditorInteractions m_interactions;
    private readonly StoryInspectorFields m_fields;
    private readonly ResourceDocumentToolbar m_toolbar;
    private readonly ResourceLabelBatchView m_batch;
    private readonly HashSet<Guid> m_selected = [];
    private IReadOnlyList<ResourceLabelEntry> m_filtered = [];
    private Guid m_document;
    private long m_revision = -1;
    private string m_search = "", m_group = "all", m_cachedSearch = "", m_cachedGroup = "", m_message = "";
    private int m_page;
    /// <summary>Uses project-owned native documents, resource selection and the existing property gesture owner.</summary>
    /// <param name="documents">The current generation's native resource document module.</param>
    /// <param name="interactions">The host's History and selection coordinator.</param>
    public ResourceLabelPanel(
        ResourceDocumentsModule documents,
        EditorInteractions interactions
    ) {
        m_documents = documents; m_interactions = interactions; m_fields = new(interactions);
        m_toolbar = new(documents, interactions, m_fields.Finish); m_batch = new(m_fields);
    }
    /// <inheritdoc />
    public override Vector2 initialSize => new(760, 720);
    /// <inheritdoc />
    protected override void OnDraw(EditorContext context)
    {
        m_fields.BeginFrame();
        try { Draw(); } catch (Exception failure) when (failure is not OutOfMemoryException) { m_message = m_fields.Error(failure); }
        finally { m_fields.EndFrame(); }
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
    }
    /// <inheritdoc />
    public ReadOnlyMemory<byte> CaptureReloadState() { m_fields.Finish(); Reset(); return m_documents.CaptureReload(); }
    /// <inheritdoc />
    public void RestoreReloadState(ReadOnlyMemory<byte> state) { m_documents.RestoreReload(state.Span); Reset(); }
    /// <inheritdoc />
    protected override void OnDetach(EditorContext context) { m_fields.Finish(); Reset(); }
    private void Reset()
    { m_document = Guid.Empty; m_revision = -1; m_selected.Clear(); m_filtered = []; m_search = m_cachedSearch = m_cachedGroup = m_message = ""; m_group = "all"; m_page = 0; m_batch.Reset(); }
    private void Draw()
    {
        if (!m_toolbar.Draw<ResourceLabelCatalogAsset>("资源整理", ".ivnlabels", static () => new(), out var catalog) || catalog is null) return;
        if (m_document != m_toolbar.documentId) { Reset(); m_document = m_toolbar.documentId; }
        ImGui.PushId(m_document.ToString("N"));
        try { DrawCatalog(catalog); }
        finally { ImGui.PopId(); }
    }
    private void DrawCatalog(ResourceLabelCatalogAsset catalog)
    {
        EditorUiSession.current.widgets.Hint(m_fields.Localize("选择 File Browser 中的原生资源后收录。分类、标签和说明属于创作记录，资源加载与身份由引擎维护。标签每行一个；分发游戏不包含本目录。"));
        string title = catalog.title;
        if (m_fields.Text("labels/title", "目录标题", ref title))
        { m_documents.Edit<ResourceLabelCatalogAsset>(m_document, m_fields.Localize("编辑资源整理标题"), value => value.title = title, m_fields.mergeKey); }
        if (ImGui.Button(m_fields.Label("收录所选资源")))
        {
            var asset = NovelResourceSelection.Get<AssetObject>(m_interactions) ?? throw new InvalidOperationException(m_fields.Localize("在 File Browser 选择已导入资源。"));
            m_fields.Finish(); m_documents.Edit<ResourceLabelCatalogAsset>(m_document, m_fields.Localize("收录资源整理记录"), candidate => ResourceLabelEditService.Set(candidate,
                candidate.entries.SingleOrDefault(entry => entry.asset?.identity.persistentId == asset.identity.persistentId)?.Copy() ?? new() { asset = asset }));
            m_message = string.Empty; return;
        }
        if (ImGui.InputText(m_fields.Label("查找路径 / 分类 / 标签 / 说明"), ref m_search, 4096)) m_page = 0;
        var groups = new[] { ("all", m_fields.Localize("全部分类")), ("empty", m_fields.Localize("未分类")) }
            .Concat(catalog.entries.Select(static entry => entry.group).Where(static group => group.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)
                .Select(static group => ("group/" + group, group))).ToArray();
        if (!groups.Any(option => option.Item1 == m_group)) m_group = "all";
        if (m_fields.Select("labels/group-filter", "按分类筛选", ref m_group, groups)) m_page = 0;
        if (ImGui.SmallButton(m_fields.Label("刷新整理列表"))) m_revision = -1;
        long revision = m_documents.GetRevision(m_document);
        if (m_revision != revision || m_cachedSearch != m_search || m_cachedGroup != m_group)
        {
            m_filtered = ResourceLabelEditService.Search(catalog, m_search, m_group == "all" ? null : m_group == "empty" ? "" : m_group[6..]);
            m_selected.IntersectWith(catalog.entries.Select(static entry => entry.asset!.identity.persistentId));
            m_revision = revision; m_cachedSearch = m_search; m_cachedGroup = m_group;
        }
        if (ImGui.SmallButton(m_fields.Label("选择全部筛选结果"))) foreach (var entry in m_filtered) m_selected.Add(entry.asset!.identity.persistentId);
        ImGui.SameLine(); if (ImGui.SmallButton(m_fields.Label("清空资源选择"))) m_selected.Clear();
        ImGui.Text(m_fields.Format("{0} 条记录 · 已选择 {1}", m_filtered.Count, m_selected.Count));
        if (m_batch.Draw(m_documents, m_document, m_selected)) { m_message = string.Empty; return; }
        int pages = Math.Max(1, (m_filtered.Count + 19) / 20); m_page = Math.Clamp(m_page, 0, pages - 1);
        if (ImGui.SmallButton(m_fields.Label("上一页##labels"))) m_page = Math.Max(0, m_page - 1);
        ImGui.SameLine(); ImGui.Text((m_page + 1) + " / " + pages); ImGui.SameLine();
        if (ImGui.SmallButton(m_fields.Label("下一页##labels"))) m_page = Math.Min(pages - 1, m_page + 1);
        if (m_filtered.Count == 0) EditorUiSession.current.widgets.Hint(m_fields.Localize("没有匹配的整理记录。清除筛选，或收录所选资源。"));
        foreach (var entry in m_filtered.Skip(m_page * 20).Take(20)) if (DrawEntry(entry)) return;
    }
    private bool DrawEntry(ResourceLabelEntry original)
    {
        var asset = original.asset!; Guid id = asset.identity.persistentId; string key = id.ToString("N");
        ImGui.PushId(key);
        try
        {
            bool selected = m_selected.Contains(id);
            if (ImGui.Checkbox(m_fields.Label("选择##label-resource"), ref selected)) { if (selected) m_selected.Add(id); else m_selected.Remove(id); }
            ImGui.SameLine();
            if (!ImGui.CollapsingHeader((asset.isMissing ? "Missing · " : "") + asset.assetPath + "###label-resource")) return false;
            ImGui.Text(id.ToString("D"));
            if (ImGui.SmallButton(m_fields.Label("选中资源供设计器使用"))) m_interactions.SetSelection(asset);
            var entry = original.Copy(); string group = entry.group, tags = string.Join('\n', entry.tags), description = entry.description;
            bool changed = m_fields.Text(key + "/group", "分类", ref group); entry.group = group;
            changed |= m_fields.MultilineText(key + "/tags", "标签（每行一个）", ref tags); entry.tags = ParagraphText.Lines(tags);
            changed |= m_fields.MultilineText(key + "/description", "资源说明", ref description); entry.description = description;
            if (changed)
            { m_documents.Edit<ResourceLabelCatalogAsset>(m_document, m_fields.Localize("编辑资源整理记录"), value => ResourceLabelEditService.Set(value, entry), m_fields.mergeKey); m_message = string.Empty; return true; }
            if (!ImGui.SmallButton(m_fields.Label("移除整理记录（保留资源）"))) return false;
            m_fields.Finish(); m_documents.Edit<ResourceLabelCatalogAsset>(m_document, m_fields.Localize("移除资源整理记录"), value => ResourceLabelEditService.Remove(value, id));
            m_selected.Remove(id); m_message = string.Empty; return true;
        }
        finally { ImGui.PopId(); }
    }
}

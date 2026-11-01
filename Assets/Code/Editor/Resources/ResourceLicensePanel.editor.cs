using System;
using System.Linq;
using System.Numerics;
using InnoEngine.Assets;
using InnoEditor.Core;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.AssetAuditing.Licenses;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Editor.Presentation;

namespace Inno.VisualNovel.Editor.Resources;

/// <summary>Edits provenance records through the shared native resource document and History services.</summary>
[EditorPanel("inno.vn.resource-licenses", "VN 资源授权记录 · Resource Licenses", order: 467, menuPath: "Authoring")]
public sealed class ResourceLicensePanel : EditorPanel, IEditorPanelReloadState
{
    private readonly ResourceDocumentsModule m_documents;
    private readonly EditorInteractions m_interactions;
    private readonly StoryInspectorFields m_fields;
    private readonly ResourceDocumentToolbar m_toolbar;
    private string m_search = "", m_message = "";
    private int m_page;
    /// <summary>Connects editable license metadata to native documents, selection and History.</summary>
    /// <param name="documents">The current native resource document service.</param>
    /// <param name="interactions">The host's selection and History owner.</param>
    public ResourceLicensePanel(
        ResourceDocumentsModule documents,
        EditorInteractions interactions
    ) { m_documents = documents; m_interactions = interactions; m_fields = new(interactions); m_toolbar = new(documents, interactions, m_fields.Finish); }
    /// <inheritdoc />
    public override Vector2 initialSize => new(740, 720);
    /// <inheritdoc />
    protected override void OnDraw(EditorContext context)
    {
        m_fields.BeginFrame();
        try { Draw(); } catch (Exception failure) when (failure is not OutOfMemoryException) { m_message = m_fields.Error(failure); }
        finally { m_fields.EndFrame(); }
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
    }
    /// <inheritdoc />
    public ReadOnlyMemory<byte> CaptureReloadState() { m_fields.Finish(); return m_documents.CaptureReload(); }
    /// <inheritdoc />
    public void RestoreReloadState(ReadOnlyMemory<byte> state) => m_documents.RestoreReload(state.Span);
    /// <inheritdoc />
    protected override void OnDetach(EditorContext context) => m_fields.Finish();
    private void Draw()
    {
        if (!m_toolbar.Draw<ResourceLicenseCatalogAsset>("授权目录", ".ivnlicenses", static () => new(), out var catalog) || catalog is null) return;
        EditorUiSession.current.widgets.Hint(m_fields.Localize("记录原生资产的作者、来源、许可、署名及修改。未完成记录可以保存；资源审计指出缺项。本目录属于创作数据，不能加入 Player 动态打包集合。"));
        if (ImGui.Button(m_fields.Label("记录所选资源")))
        {
            var selected = NovelResourceSelection.Get<AssetObject>(m_interactions) ?? throw new InvalidOperationException(m_fields.Localize("在 File Browser 选择已导入资源。"));
            m_fields.Finish(); m_documents.Edit<ResourceLicenseCatalogAsset>(m_toolbar.documentId, m_fields.Localize("记录资源授权"), candidate => ResourceLicenseEditService.Set(candidate,
                candidate.entries.SingleOrDefault(item => item.asset?.identity.persistentId == selected.identity.persistentId)?.Copy() ?? new() { asset = selected }));
        }
        ImGui.InputText(m_fields.Label("查找资源 / 作者 / 许可"), ref m_search, 4096);
        var entries = catalog.entries.Where(entry => m_search.Length == 0 || (entry.asset?.assetPath.ToString() ?? "").Contains(m_search, StringComparison.OrdinalIgnoreCase)
            || entry.author.Contains(m_search, StringComparison.OrdinalIgnoreCase) || entry.license.Contains(m_search, StringComparison.OrdinalIgnoreCase)).ToArray();
        int pages = Math.Max(1, (entries.Length + 19) / 20); m_page = Math.Clamp(m_page, 0, pages - 1);
        if (ImGui.Button(m_fields.Label("上一页##license"))) m_page = Math.Max(0, m_page - 1);
        ImGui.SameLine(); ImGui.Text((m_page + 1) + " / " + pages); ImGui.SameLine(); if (ImGui.Button(m_fields.Label("下一页##license"))) m_page = Math.Min(pages - 1, m_page + 1);
        foreach (var original in entries.Skip(m_page * 20).Take(20))
        {
            var resource = original.asset ?? throw new InvalidOperationException(m_fields.Localize("授权记录缺少资源身份。"));
            string id = resource.identity.persistentId.ToString("D");
            if (!ImGui.CollapsingHeader((resource.isMissing ? "Missing · " : "") + resource.assetPath + "##" + id)) continue;
            var entry = original.Copy(); string author = entry.author, source = entry.source, license = entry.license, notice = entry.notice, changes = entry.modifications;
            bool changed = m_fields.Text(id + "/author", m_fields.Localize("作者 / 权利人"), ref author); entry.author = author;
            changed |= m_fields.Text(id + "/source", m_fields.Localize("来源链接 / 原创说明"), ref source); entry.source = source;
            changed |= m_fields.Text(id + "/license", m_fields.Localize("许可名称 / 标识"), ref license); entry.license = license;
            bool confirmed = entry.redistributionConfirmed, required = entry.attributionRequired;
            changed |= m_fields.Toggle(id + "/redistribution", "我已确认本游戏的计划分发权限", ref confirmed); entry.redistributionConfirmed = confirmed;
            changed |= m_fields.Toggle(id + "/attribution", "要求署名 / 附带声明", ref required); entry.attributionRequired = required;
            changed |= m_fields.MultilineText(id + "/notice", m_fields.Localize("署名或许可正文"), ref notice); entry.notice = notice;
            changed |= m_fields.MultilineText(id + "/modifications", m_fields.Localize("修改情况"), ref changes); entry.modifications = changes;
            if (changed) m_documents.Edit<ResourceLicenseCatalogAsset>(m_toolbar.documentId, m_fields.Localize("编辑资源授权记录"), candidate => ResourceLicenseEditService.Set(candidate, entry), m_fields.mergeKey);
            if (ImGui.Button(m_fields.Caption("移除记录（保留资源）##" + id, m_fields.Localize("移除记录（保留资源）##") + id)))
            { m_fields.Finish(); m_documents.Edit<ResourceLicenseCatalogAsset>(m_toolbar.documentId, m_fields.Localize("移除资源授权记录"), candidate => ResourceLicenseEditService.Remove(candidate, resource.identity.persistentId)); }
        }
    }
}

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
[EditorPanel("inno.vn.resource-licenses", "VN Resource Licenses", order: 467, defaultOpen: false, menuPath: "Authoring")]
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
        try { Draw(); } catch (Exception failure) when (failure is not OutOfMemoryException) { m_message = (failure).Message; }
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
        if (!m_toolbar.Draw<ResourceLicenseCatalogAsset>("License catalog", ".ivnlicenses", static () => new(), out var catalog) || catalog is null) return;
        EditorUiSession.current.widgets.Hint("Record each asset's author, source, license, attribution and modifications. Incomplete entries can be saved; resource audits identify missing information. This catalog is authoring data and cannot be added to the Player's explicit packaging collection.");
        if (ImGui.Button("Record selected resource"))
        {
            var selected = NovelResourceSelection.Get<AssetObject>(m_interactions) ?? throw new InvalidOperationException("Select an imported resource in File Browser.");
            m_fields.Finish(); m_documents.Edit<ResourceLicenseCatalogAsset>(m_toolbar.documentId, "Record resource license", candidate => ResourceLicenseEditService.Set(candidate,
                candidate.entries.SingleOrDefault(item => item.asset?.identity.persistentId == selected.identity.persistentId)?.Copy() ?? new() { asset = selected }));
        }
        ImGui.InputText("Find resource / author / license", ref m_search, 4096);
        var entries = catalog.entries.Where(entry => m_search.Length == 0 || (entry.asset?.assetPath.ToString() ?? "").Contains(m_search, StringComparison.OrdinalIgnoreCase)
            || entry.author.Contains(m_search, StringComparison.OrdinalIgnoreCase) || entry.license.Contains(m_search, StringComparison.OrdinalIgnoreCase)).ToArray();
        int pages = Math.Max(1, (entries.Length + 19) / 20); m_page = Math.Clamp(m_page, 0, pages - 1);
        if (ImGui.Button("Previous page##license")) m_page = Math.Max(0, m_page - 1);
        ImGui.SameLine(); ImGui.Text((m_page + 1) + " / " + pages); ImGui.SameLine(); if (ImGui.Button("Next page##license")) m_page = Math.Min(pages - 1, m_page + 1);
        foreach (var original in entries.Skip(m_page * 20).Take(20))
        {
            var resource = original.asset ?? throw new InvalidOperationException("The license entry has no resource identity.");
            string id = resource.identity.persistentId.ToString("D");
            if (!ImGui.CollapsingHeader((resource.isMissing ? "Missing · " : "") + resource.assetPath + "##" + id)) continue;
            var entry = original.Copy(); string author = entry.author, source = entry.source, license = entry.license, notice = entry.notice, changes = entry.modifications;
            bool changed = m_fields.Text(id + "/author", "Author / rights holder", ref author); entry.author = author;
            changed |= m_fields.Text(id + "/source", "Source link / original-work description", ref source); entry.source = source;
            changed |= m_fields.Text(id + "/license", "License name / identifier", ref license); entry.license = license;
            bool confirmed = entry.redistributionConfirmed, required = entry.attributionRequired;
            changed |= m_fields.Toggle(id + "/redistribution", "I have confirmed permission for this game's planned distribution", ref confirmed); entry.redistributionConfirmed = confirmed;
            changed |= m_fields.Toggle(id + "/attribution", "Attribution / notice required", ref required); entry.attributionRequired = required;
            changed |= m_fields.MultilineText(id + "/notice", "Attribution or license text", ref notice); entry.notice = notice;
            changed |= m_fields.MultilineText(id + "/modifications", "Modifications", ref changes); entry.modifications = changes;
            if (changed) m_documents.Edit<ResourceLicenseCatalogAsset>(m_toolbar.documentId, "Edit resource license entry", candidate => ResourceLicenseEditService.Set(candidate, entry), m_fields.mergeKey);
            if (ImGui.Button("Remove entry (keep resource)##" + id))
            { m_fields.Finish(); m_documents.Edit<ResourceLicenseCatalogAsset>(m_toolbar.documentId, "Remove resource license entry", candidate => ResourceLicenseEditService.Remove(candidate, resource.identity.persistentId)); }
        }
    }
}

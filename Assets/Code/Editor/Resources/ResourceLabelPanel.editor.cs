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
[EditorPanel("inno.vn.resource-labels", "VN Resource Labels", order: 469, defaultOpen: false, menuPath: "Authoring")]
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
        try { Draw(); } catch (Exception failure) when (failure is not OutOfMemoryException) { m_message = (failure).Message; }
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
        if (!m_toolbar.Draw<ResourceLabelCatalogAsset>("Resource annotations", ".ivnlabels", static () => new(), out var catalog) || catalog is null) return;
        if (m_document != m_toolbar.documentId) { Reset(); m_document = m_toolbar.documentId; }
        ImGui.PushId(m_document.ToString("N"));
        try { DrawCatalog(catalog); }
        finally { ImGui.PopId(); }
    }
    private void DrawCatalog(ResourceLabelCatalogAsset catalog)
    {
        EditorUiSession.current.widgets.Hint("Select a native resource in File Browser, then add it. Categories, tags and descriptions are authoring notes; the engine owns resource loading and identity. Enter one tag per line. This catalog is excluded from game content.");
        string title = catalog.title;
        if (m_fields.Text("labels/title", "Catalog title", ref title))
        { m_documents.Edit<ResourceLabelCatalogAsset>(m_document, "Edit resource annotation title", value => value.title = title, m_fields.mergeKey); }
        if (ImGui.Button("Add selected resource"))
        {
            var asset = NovelResourceSelection.Get<AssetObject>(m_interactions) ?? throw new InvalidOperationException("Select an imported resource in File Browser.");
            m_fields.Finish(); m_documents.Edit<ResourceLabelCatalogAsset>(m_document, "Add resource annotation", candidate => ResourceLabelEditService.Set(candidate,
                candidate.entries.SingleOrDefault(entry => entry.asset?.identity.persistentId == asset.identity.persistentId)?.Copy() ?? new() { asset = asset }));
            m_message = string.Empty; return;
        }
        if (ImGui.InputText("Search path / category / tags / description", ref m_search, 4096)) m_page = 0;
        var groups = new[] { ("all", "All categories"), ("empty", "Ungrouped") }
            .Concat(catalog.entries.Select(static entry => entry.group).Where(static group => group.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)
                .Select(static group => ("group/" + group, group))).ToArray();
        if (!groups.Any(option => option.Item1 == m_group)) m_group = "all";
        if (m_fields.Select("labels/group-filter", "Filter by category", ref m_group, groups)) m_page = 0;
        if (ImGui.SmallButton("Refresh annotations")) m_revision = -1;
        long revision = m_documents.GetRevision(m_document);
        if (m_revision != revision || m_cachedSearch != m_search || m_cachedGroup != m_group)
        {
            m_filtered = ResourceLabelEditService.Search(catalog, m_search, m_group == "all" ? null : m_group == "empty" ? "" : m_group[6..]);
            m_selected.IntersectWith(catalog.entries.Select(static entry => entry.asset!.identity.persistentId));
            m_revision = revision; m_cachedSearch = m_search; m_cachedGroup = m_group;
        }
        if (ImGui.SmallButton("Select all filtered resources")) foreach (var entry in m_filtered) m_selected.Add(entry.asset!.identity.persistentId);
        ImGui.SameLine(); if (ImGui.SmallButton("Clear resource selection")) m_selected.Clear();
        ImGui.Text(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0} records · {1} selected", m_filtered.Count, m_selected.Count));
        if (m_batch.Draw(m_documents, m_document, m_selected)) { m_message = string.Empty; return; }
        int pages = Math.Max(1, (m_filtered.Count + 19) / 20); m_page = Math.Clamp(m_page, 0, pages - 1);
        if (ImGui.SmallButton("Previous##labels")) m_page = Math.Max(0, m_page - 1);
        ImGui.SameLine(); ImGui.Text((m_page + 1) + " / " + pages); ImGui.SameLine();
        if (ImGui.SmallButton("Next##labels")) m_page = Math.Min(pages - 1, m_page + 1);
        if (m_filtered.Count == 0) EditorUiSession.current.widgets.Hint("No matching annotations. Clear the filter or add the selected resource.");
        foreach (var entry in m_filtered.Skip(m_page * 20).Take(20)) if (DrawEntry(entry)) return;
    }
    private bool DrawEntry(ResourceLabelEntry original)
    {
        var asset = original.asset!; Guid id = asset.identity.persistentId; string key = id.ToString("N");
        ImGui.PushId(key);
        try
        {
            bool selected = m_selected.Contains(id);
            if (ImGui.Checkbox("Select##label-resource", ref selected)) { if (selected) m_selected.Add(id); else m_selected.Remove(id); }
            ImGui.SameLine();
            if (!ImGui.CollapsingHeader((asset.isMissing ? "Missing · " : "") + asset.assetPath + "###label-resource")) return false;
            ImGui.Text(id.ToString("D"));
            if (ImGui.SmallButton("Select resource for designers")) m_interactions.SetSelection(asset);
            var entry = original.Copy(); string group = entry.group, tags = string.Join('\n', entry.tags), description = entry.description;
            bool changed = m_fields.Text(key + "/group", "Category", ref group); entry.group = group;
            changed |= m_fields.MultilineText(key + "/tags", "Tags (one per line)", ref tags); entry.tags = ParagraphText.Lines(tags);
            changed |= m_fields.MultilineText(key + "/description", "Resource description", ref description); entry.description = description;
            if (changed)
            { m_documents.Edit<ResourceLabelCatalogAsset>(m_document, "Edit resource annotation", value => ResourceLabelEditService.Set(value, entry), m_fields.mergeKey); m_message = string.Empty; return true; }
            if (!ImGui.SmallButton("Remove annotation (keep resource)")) return false;
            m_fields.Finish(); m_documents.Edit<ResourceLabelCatalogAsset>(m_document, "Remove resource annotation", value => ResourceLabelEditService.Remove(value, id));
            m_selected.Remove(id); m_message = string.Empty; return true;
        }
        finally { ImGui.PopId(); }
    }
}

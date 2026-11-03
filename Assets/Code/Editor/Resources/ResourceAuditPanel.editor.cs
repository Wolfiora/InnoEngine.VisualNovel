using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using InnoEngine.Assets;
using InnoEngine.Graphs;
using InnoEditor.Core;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.AssetAuditing;
using Inno.VisualNovel.Authoring.AssetAuditing.Licenses;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.Publishing;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Editor.Localization;

namespace Inno.VisualNovel.Editor.Resources;

/// <summary>Reviews native usage, source byte duplicates and provenance without deleting or rewriting resources.</summary>
[EditorPanel("inno.vn.resource-audit", "VN Resource Audit", order: 468, defaultOpen: false, menuPath: "Authoring")]
public sealed class ResourceAuditPanel : EditorPanel
{
    private readonly ResourceDocumentsModule m_documents;
    private readonly EditorInteractions m_interactions;
    private ProjectResourceAudit? m_job;
    private ResourceAuditReport? m_report, m_filteredReport;
    private NovelProjectAsset? m_usageProject;
    private NovelPublishReport? m_usageClosure;
    private ResourceUsageSearch? m_usage;
    private int m_usagePage;
    private ResourceAuditItem[] m_filtered = [];
    private HashSet<AssetPath> m_duplicates = [];
    private HashSet<string> m_problemPaths = new(StringComparer.Ordinal);
    private Guid m_licenseDocument;
    private string m_licenseTitle = "Not selected", m_search = "", m_filter = "all", m_filterKey = "", m_message = "";
    private int m_page;
    /// <summary>Uses only public native assets, shared document editing and engine selection.</summary>
    /// <param name="documents">The native provenance document owner.</param>
    /// <param name="interactions">The current editor selection and History service.</param>
    public ResourceAuditPanel(
        ResourceDocumentsModule documents,
        EditorInteractions interactions
    ) { m_documents = documents; m_interactions = interactions;  }
    /// <inheritdoc />
    public override Vector2 initialSize => new(1000, 740);
    /// <inheritdoc />
    protected override void OnDraw(EditorContext context)
    {
        try { Draw(context); } catch (Exception failure) when (failure is not OutOfMemoryException) { m_message = (failure).Message; }
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
    }
    /// <inheritdoc />
    protected override void OnDetach(EditorContext context) { m_job?.Dispose(); m_usage?.Dispose(); m_usage = null; m_usageProject = null; m_usageClosure = null; m_job = null; m_report = m_filteredReport = null; m_filtered = []; m_duplicates.Clear(); m_problemPaths.Clear(); }
    private void Draw(EditorContext context)
    {
        EditorUiSession.current.widgets.Hint("Audit the selected game's native dependency closure and the current project's Assets sources. Identical bytes do not make assets interchangeable. Unreferenced means unreferenced by this game; include dynamic references in the explicit packaging collection. This panel does not delete resources.");
        if (ImGui.Button("Use selected license catalog"))
        {
            var selected = NovelResourceSelection.Get<ResourceLicenseCatalogAsset>(m_interactions) ?? throw new InvalidOperationException("Select an .ivnlicenses asset, or create a license catalog first.");
            var document = m_documents.Open(selected.assetPath); m_licenseDocument = document.documentId; m_licenseTitle = selected.title;
        }
        ImGui.SameLine(); if (ImGui.Button("Create and use license catalog")) { var document = m_documents.Create(new ResourceLicenseCatalogAsset()); m_licenseDocument = document.documentId; m_licenseTitle = document.title; }
        ImGui.Text("License catalog draft · " + m_licenseTitle);
        if (ImGui.Button("Audit selected game (saved content)"))
        {
            var game = NovelResourceSelection.Get<NovelProjectAsset>(m_interactions) ?? throw new InvalidOperationException("Select a saved game definition.");
            var licenses = m_licenseDocument == Guid.Empty ? null : m_documents.Read<ResourceLicenseCatalogAsset>(m_licenseDocument);
            var closure = NovelPublishPreflight.Check(game);
            var candidate = new ProjectResourceAudit(context.projectDirectory, closure, licenses);
            m_job?.Dispose(); m_job = candidate; m_report = m_filteredReport = null; m_filtered = []; m_page = 0;
            m_usage?.Dispose(); m_usage = null; m_usageProject = game; m_usageClosure = closure;
        }
        if (m_job is not null)
        {
            if (!m_job.completed && !m_job.cancelled && ImGui.Button("Cancel audit")) { m_job.Cancel(); Capture(); }
            m_job.Step(8);
            ImGui.Text((m_job.cancelled ? "Cancelled" : m_job.completed ? "Enumeration complete" : "Scanning / hashing") + " · Visited: " + m_job.visited + " entries · Resources checked: " + m_job.items.Count);
            if (m_job.currentSource is { } current) ImGui.Text("Reading · " + current);
            if (ImGui.Button("View current results")) Capture();
            if (m_job.completed && m_report is not { finished: true }) Capture();
        }
        if (m_report is null) return;
        DrawUsage();
        ImGui.Text(m_report.title + " · Resources: " + m_report.items.Count + " · Duplicate groups: " + m_report.duplicateGroups.Count + " · Issues: " + m_report.issues.Count);
        if (!m_report.inventoryComplete || !m_report.closureComplete) EditorUiSession.current.widgets.Hint("The report is incomplete or the dependency closure is unavailable. Review Unknown entries and reported issues; do not use this as an automatic cleanup list.");
        if (ImGui.Button("Save report and attribution draft"))
        {
            string directory = Path.Combine(context.projectDirectory, "artifacts", "Publishing"); Directory.CreateDirectory(directory);
            string stem = Path.Combine(directory, "Resources-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
            File.WriteAllText(stem + ".md", m_report.ToMarkdown()); File.WriteAllText(stem + "-Notices.txt", m_report.NoticesText()); m_message = "Report written: " + stem;
        }
        ImGui.InputText("Find resource path", ref m_search, 4096);
        var filters = new[] { ("all", "All"), ("used", "Referenced"), ("unused", "Not referenced by current game"), ("duplicate", "Identical bytes"), ("issues", "Has issues"), ("unknown", "Unknown"), ("editor", "Authoring data") };
        if (ImGui.BeginCombo("Resource filter", filters.Single(item => item.Item1 == m_filter).Item2, default))
        {
            try { foreach (var (id, label) in filters) if (ImGui.Selectable(label, m_filter == id)) m_filter = id; }
            finally { ImGui.EndCombo(); }
        }
        string key = m_filter + "\n" + m_search;
        if (!ReferenceEquals(m_filteredReport, m_report) || m_filterKey != key)
        {
            m_filteredReport = m_report; m_filterKey = key; m_page = 0;
            m_filtered = m_report.items.Where(item => (m_search.Length == 0 || item.path.ToString().Contains(m_search, StringComparison.OrdinalIgnoreCase)) && (m_filter switch
            { "used" => item.use == ResourceAuditUse.Referenced, "unused" => item.use == ResourceAuditUse.NotReferenced, "duplicate" => m_duplicates.Contains(item.path),
                "issues" => item.message.Length > 0 || m_problemPaths.Contains(item.path.ToString()), "unknown" => item.use == ResourceAuditUse.Unknown, "editor" => item.use == ResourceAuditUse.AuthoringOnly, _ => true })).ToArray();
        }
        int pages = Math.Max(1, (m_filtered.Length + 39) / 40); m_page = Math.Clamp(m_page, 0, pages - 1);
        if (ImGui.Button("Previous page##audit")) m_page = Math.Max(0, m_page - 1);
        ImGui.SameLine(); ImGui.Text((m_page + 1) + " / " + pages + " · " + m_filtered.Length); ImGui.SameLine(); if (ImGui.Button("Next page##audit")) m_page = Math.Min(pages - 1, m_page + 1);
        foreach (var item in m_filtered.Skip(m_page * 40).Take(40))
        {
            if (ImGui.Selectable(item.path + " · " + item.bytes + " source bytes · " + ResourceInventoryRules.DescribeUse(item.use)) && Assets.TryLoad<AssetObject>(item.id, out var loaded)) m_interactions.SetSelection(loaded);
            if (item.message.Length > 0) EditorUiSession.current.widgets.Hint(item.message);
            if (item.referrers.Count > 0) ImGui.Text("Referenced by · " + string.Join("；", item.referrers));
            if (item.id != Guid.Empty && m_usageProject is not null && m_usageClosure is not null && ImGui.SmallButton("Find story / control locations##" + item.path))
            {
                if (m_usageProject.buildFingerprint != m_usageClosure.fingerprint) throw new InvalidOperationException("Game content changed. Audit it again before finding locations.");
                var usage = new ResourceUsageSearch(m_usageProject, m_usageClosure, item.id); m_usage?.Dispose(); m_usage = usage; m_usagePage = 0;
            }
            if (m_licenseDocument != Guid.Empty && item.id != Guid.Empty && ImGui.SmallButton("Record in current license catalog##" + item.path))
            {
                var selected = Assets.Load<AssetObject>(item.id);
                m_documents.Edit<ResourceLicenseCatalogAsset>(m_licenseDocument, "Record audited resource license", candidate => ResourceLicenseEditService.Set(candidate,
                    candidate.entries.SingleOrDefault(entry => entry.asset?.identity.persistentId == item.id)?.Copy() ?? new() { asset = selected }));
                m_message = "Added to the license catalog. Complete the entry in VN Resource Licenses, save it, then audit again.";
            }
        }
        if (ImGui.CollapsingHeader("Audit issues")) foreach (var issue in m_report.issues) EditorUiSession.current.widgets.Hint(issue.level + " · " + issue.location + " · " + issue.message);
    }
    private void Capture()
    {
        m_report = m_job!.Capture(); m_duplicates = m_report.duplicateGroups.SelectMany(static group => group).Select(static item => item.path).ToHashSet();
        m_problemPaths = m_report.issues.Select(static issue => issue.location).ToHashSet(StringComparer.Ordinal); m_filteredReport = null;
    }
    private void DrawUsage()
    {
        if (m_usage is null) return;
        m_usage.Step(256);
        ImGui.Text("Usage locations · " + (m_usage.cancelled ? "Cancelled" : m_usage.completed ? "End" : "Searching") + " · Candidates: " + m_usage.scanned + " · Locations: " + m_usage.results.Count);
        if (!m_usage.completed && !m_usage.cancelled && ImGui.SmallButton("Cancel location search")) m_usage.Cancel();
        if (m_usage.error.Length > 0) EditorUiSession.current.widgets.Hint(m_usage.failure is { } cause ? (cause).Message : m_usage.error);
        EditorUiSession.current.widgets.Hint("A library containing the target indicates a declared dependency. It does not prove a route displays a particular variant. Check dynamic scripts together with additional packaging resources and extension implementations.");
        int pages = Math.Max(1, (m_usage.results.Count + 19) / 20); m_usagePage = Math.Clamp(m_usagePage, 0, pages - 1);
        if (ImGui.SmallButton("Previous page##usage")) m_usagePage = Math.Max(0, m_usagePage - 1);
        ImGui.SameLine(); ImGui.Text((m_usagePage + 1) + " / " + pages); ImGui.SameLine(); if (ImGui.SmallButton("Next page##usage")) m_usagePage = Math.Min(pages - 1, m_usagePage + 1);
        foreach (var (location, index) in m_usage.results.Skip(m_usagePage * 20).Take(20).Select((location, index) => (location, index)))
        {
            ImGui.Text(location.description + " · " + location.ownerPath + (location.nodeId.Length > 0 ? " / " + location.nodeId : location.elementId.Length > 0 ? " / " + location.elementId : ""));
            if (location.ownerId == Guid.Empty || !ImGui.SmallButton("Locate##usage/" + index)) continue;
            var owner = Assets.Load<AssetObject>(location.ownerId);
            if (owner.isMissing) throw new InvalidOperationException("The source asset is unavailable. Search again.");
            if (location.nodeId.Length > 0)
            {
                if (!m_interactions.TryGetModule<StoryDocumentsModule>(out var stories) || stories is null) throw new InvalidOperationException("The chapter document service is unavailable.");
                var document = stories.Open(owner.assetPath);
                if (!stories.Read(document.documentId).graph.document.nodes.Any(node => node.id.value == location.nodeId)) throw new InvalidOperationException("The source node changed or was removed. Search again.");
                StoryNodeSelection.Select(m_interactions, document.documentId, location.nodeId);
            }
            else if (location.elementId.Length > 0)
            {
                var document = m_documents.Open(owner.assetPath);
                if (!ScreenEditService.Flatten(m_documents.Read<Model.Interface.ScreenAsset>(document.documentId)).Any(row => row.element.id == location.elementId)) throw new InvalidOperationException("The source control changed or was removed. Search again.");
                ScreenElementSelection.Select(m_interactions, document.documentId, location.elementId);
            }
            else m_interactions.SetSelection(owner);
        }
        ImGui.Separator();
    }
}

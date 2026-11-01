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
[EditorPanel("inno.vn.resource-audit", "VN 资源审计 · Resource Audit", order: 468, menuPath: "Authoring")]
public sealed class ResourceAuditPanel : EditorPanel
{
    private readonly EditorTextView m_text;
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
    private string m_licenseTitle = "未选择", m_search = "", m_filter = "all", m_filterKey = "", m_message = "";
    private int m_page;
    /// <summary>Uses only public native assets, shared document editing and engine selection.</summary>
    /// <param name="documents">The native provenance document owner.</param>
    /// <param name="interactions">The current editor selection and History service.</param>
    public ResourceAuditPanel(
        ResourceDocumentsModule documents,
        EditorInteractions interactions
    ) { m_documents = documents; m_interactions = interactions; m_text = new(interactions); }
    /// <inheritdoc />
    public override Vector2 initialSize => new(1000, 740);
    /// <inheritdoc />
    protected override void OnDraw(EditorContext context)
    {
        try { Draw(context); } catch (Exception failure) when (failure is not OutOfMemoryException) { m_message = m_text.Error(failure); }
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
    }
    /// <inheritdoc />
    protected override void OnDetach(EditorContext context) { m_job?.Dispose(); m_usage?.Dispose(); m_usage = null; m_usageProject = null; m_usageClosure = null; m_job = null; m_report = m_filteredReport = null; m_filtered = []; m_duplicates.Clear(); m_problemPaths.Clear(); }
    private void Draw(EditorContext context)
    {
        EditorUiSession.current.widgets.Hint(m_text.Localize("审计选定游戏的原生依赖闭包和当前项目 Assets 源。字节重复不等于资产可互换；未引用仅相对该游戏，动态引用须显式加入打包集合。本面板不删除资源。"));
        if (ImGui.Button(m_text.Label("使用所选授权目录")))
        {
            var selected = NovelResourceSelection.Get<ResourceLicenseCatalogAsset>(m_interactions) ?? throw new InvalidOperationException(m_text.Localize("选择 .ivnlicenses，或先创建授权目录。"));
            var document = m_documents.Open(selected.assetPath); m_licenseDocument = document.documentId; m_licenseTitle = selected.title;
        }
        ImGui.SameLine(); if (ImGui.Button(m_text.Label("创建并使用授权目录"))) { var document = m_documents.Create(new ResourceLicenseCatalogAsset()); m_licenseDocument = document.documentId; m_licenseTitle = document.title; }
        ImGui.Text(m_text.Localize("授权目录草稿 · ") + m_licenseTitle);
        if (ImGui.Button(m_text.Label("审计所选游戏（保存内容）")))
        {
            var game = NovelResourceSelection.Get<NovelProjectAsset>(m_interactions) ?? throw new InvalidOperationException(m_text.Localize("选择已保存游戏定义。"));
            var licenses = m_licenseDocument == Guid.Empty ? null : m_documents.Read<ResourceLicenseCatalogAsset>(m_licenseDocument);
            var closure = NovelPublishPreflight.Check(game);
            var candidate = new ProjectResourceAudit(context.projectDirectory, closure, licenses);
            m_job?.Dispose(); m_job = candidate; m_report = m_filteredReport = null; m_filtered = []; m_page = 0;
            m_usage?.Dispose(); m_usage = null; m_usageProject = game; m_usageClosure = closure;
        }
        if (m_job is not null)
        {
            if (!m_job.completed && !m_job.cancelled && ImGui.Button(m_text.Label("取消审计"))) { m_job.Cancel(); Capture(); }
            m_job.Step(8);
            ImGui.Text((m_job.cancelled ? m_text.Localize("已取消") : m_job.completed ? m_text.Localize("枚举结束") : m_text.Localize("扫描 / 哈希中")) + m_text.Localize(" · 已访问 ") + m_job.visited + m_text.Localize(" 项 · 已检查资源 ") + m_job.items.Count);
            if (m_job.currentSource is { } current) ImGui.Text(m_text.Localize("正在读取 · ") + current);
            if (ImGui.Button(m_text.Label("查看当前结果"))) Capture();
            if (m_job.completed && m_report is not { finished: true }) Capture();
        }
        if (m_report is null) return;
        DrawUsage();
        ImGui.Text(m_report.title + m_text.Localize(" · 资源 ") + m_report.items.Count + m_text.Localize(" · 重复组 ") + m_report.duplicateGroups.Count + m_text.Localize(" · 问题 ") + m_report.issues.Count);
        if (!m_report.inventoryComplete || !m_report.closureComplete) EditorUiSession.current.widgets.Hint(m_text.Localize("报告不完整或闭包不可用，请结合 Unknown 和检查问题处理，不能直接当作清理清单。"));
        if (ImGui.Button(m_text.Label("保存报告与署名草稿")))
        {
            string directory = Path.Combine(context.projectDirectory, "artifacts", "Publishing"); Directory.CreateDirectory(directory);
            string stem = Path.Combine(directory, "Resources-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
            File.WriteAllText(stem + ".md", m_report.ToMarkdown()); File.WriteAllText(stem + "-Notices.txt", m_report.NoticesText()); m_message = m_text.Localize("报告已写入：") + stem;
        }
        ImGui.InputText(m_text.Label("查找资源路径"), ref m_search, 4096);
        var filters = new[] { ("all", "全部"), ("used", "已引用"), ("unused", "当前游戏未引用"), ("duplicate", "字节重复"), ("issues", "存在问题"), ("unknown", "无法判断"), ("editor", "创作数据") };
        if (ImGui.BeginCombo(m_text.Label("资源筛选"), filters.Single(item => item.Item1 == m_filter).Item2, default))
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
        if (ImGui.Button(m_text.Label("上一页##audit"))) m_page = Math.Max(0, m_page - 1);
        ImGui.SameLine(); ImGui.Text((m_page + 1) + " / " + pages + " · " + m_filtered.Length); ImGui.SameLine(); if (ImGui.Button(m_text.Label("下一页##audit"))) m_page = Math.Min(pages - 1, m_page + 1);
        foreach (var item in m_filtered.Skip(m_page * 40).Take(40))
        {
            if (ImGui.Selectable(m_text.Caption(item.path + " · " + item.bytes + " 源字节 · " + ResourceInventoryRules.DescribeUse(item.use), item.path + " · " + item.bytes + m_text.Localize(" 源字节 · ") + ResourceInventoryRules.DescribeUse(item.use))) && Assets.TryLoad<AssetObject>(item.id, out var loaded)) m_interactions.SetSelection(loaded);
            if (item.message.Length > 0) EditorUiSession.current.widgets.Hint(item.message);
            if (item.referrers.Count > 0) ImGui.Text(m_text.Localize("引用来源 · ") + string.Join("；", item.referrers));
            if (item.id != Guid.Empty && m_usageProject is not null && m_usageClosure is not null && ImGui.SmallButton(m_text.Caption("查找剧情 / 控件位置##" + item.path, m_text.Localize("查找剧情 / 控件位置##") + item.path)))
            {
                if (m_usageProject.buildFingerprint != m_usageClosure.fingerprint) throw new InvalidOperationException(m_text.Localize("游戏内容已改变，请重新审计后查找位置。"));
                var usage = new ResourceUsageSearch(m_usageProject, m_usageClosure, item.id); m_usage?.Dispose(); m_usage = usage; m_usagePage = 0;
            }
            if (m_licenseDocument != Guid.Empty && item.id != Guid.Empty && ImGui.SmallButton(m_text.Caption("在当前授权目录记录##" + item.path, m_text.Localize("在当前授权目录记录##") + item.path)))
            {
                var selected = Assets.Load<AssetObject>(item.id);
                m_documents.Edit<ResourceLicenseCatalogAsset>(m_licenseDocument, m_text.Localize("记录审计资源授权"), candidate => ResourceLicenseEditService.Set(candidate,
                    candidate.entries.SingleOrDefault(entry => entry.asset?.identity.persistentId == item.id)?.Copy() ?? new() { asset = selected }));
                m_message = m_text.Localize("已加入授权目录；在 VN 资源授权记录面板填写，保存后重新审计。");
            }
        }
        if (ImGui.CollapsingHeader(m_text.Label("审计问题"))) foreach (var issue in m_report.issues) EditorUiSession.current.widgets.Hint(issue.level + " · " + issue.location + " · " + issue.message);
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
        ImGui.Text(m_text.Localize("使用位置 · ") + (m_usage.cancelled ? m_text.Localize("已取消") : m_usage.completed ? m_text.Localize("结束") : m_text.Localize("查找中")) + m_text.Localize(" · 候选 ") + m_usage.scanned + m_text.Localize(" · 位置 ") + m_usage.results.Count);
        if (!m_usage.completed && !m_usage.cancelled && ImGui.SmallButton(m_text.Label("取消位置查找"))) m_usage.Cancel();
        if (m_usage.error.Length > 0) EditorUiSession.current.widgets.Hint(m_usage.failure is { } cause ? m_text.Error(cause) : m_usage.error);
        EditorUiSession.current.widgets.Hint(m_text.Localize("“包含目标的资源库”表示声明依赖关系，不保证该路线实际显示了某张差分。动态脚本仍需结合附加打包和扩展实现检查。"));
        int pages = Math.Max(1, (m_usage.results.Count + 19) / 20); m_usagePage = Math.Clamp(m_usagePage, 0, pages - 1);
        if (ImGui.SmallButton(m_text.Label("上一页##usage"))) m_usagePage = Math.Max(0, m_usagePage - 1);
        ImGui.SameLine(); ImGui.Text((m_usagePage + 1) + " / " + pages); ImGui.SameLine(); if (ImGui.SmallButton(m_text.Label("下一页##usage"))) m_usagePage = Math.Min(pages - 1, m_usagePage + 1);
        foreach (var (location, index) in m_usage.results.Skip(m_usagePage * 20).Take(20).Select((location, index) => (location, index)))
        {
            ImGui.Text(location.description + " · " + location.ownerPath + (location.nodeId.Length > 0 ? " / " + location.nodeId : location.elementId.Length > 0 ? " / " + location.elementId : ""));
            if (location.ownerId == Guid.Empty || !ImGui.SmallButton(m_text.Caption("定位##usage/" + index, m_text.Localize("定位##usage/") + index))) continue;
            var owner = Assets.Load<AssetObject>(location.ownerId);
            if (owner.isMissing) throw new InvalidOperationException(m_text.Localize("来源资产已不可用，请重新查找。"));
            if (location.nodeId.Length > 0)
            {
                if (!m_interactions.TryGetModule<StoryDocumentsModule>(out var stories) || stories is null) throw new InvalidOperationException(m_text.Localize("章节文档服务不可用。"));
                var document = stories.Open(owner.assetPath);
                if (!stories.Read(document.documentId).graph.document.nodes.Any(node => node.id.value == location.nodeId)) throw new InvalidOperationException(m_text.Localize("来源节点已改变或删除，请重新查找。"));
                StoryNodeSelection.Select(m_interactions, document.documentId, location.nodeId);
            }
            else if (location.elementId.Length > 0)
            {
                var document = m_documents.Open(owner.assetPath);
                if (!ScreenEditService.Flatten(m_documents.Read<Model.Interface.ScreenAsset>(document.documentId)).Any(row => row.element.id == location.elementId)) throw new InvalidOperationException(m_text.Localize("来源控件已改变或删除，请重新查找。"));
                ScreenElementSelection.Select(m_interactions, document.documentId, location.elementId);
            }
            else m_interactions.SetSelection(owner);
        }
        ImGui.Separator();
    }
}

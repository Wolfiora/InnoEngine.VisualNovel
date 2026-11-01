using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using InnoEngine.Assets;
using Inno.VisualNovel.Authoring.AssetAuditing.Licenses;
using Inno.VisualNovel.Authoring.Publishing;

namespace Inno.VisualNovel.Authoring.AssetAuditing;

/// <summary>Compares a temporary project source inventory with an existing native closure, hashing files in bounded owner-thread chunks.</summary>
public sealed class ProjectResourceAudit : IDisposable
{
    private readonly string m_sourceRoot;
    private readonly NovelPublishReport m_closure;
    private readonly bool m_closureComplete;
    private readonly Dictionary<Guid, PublishAsset> m_referenced;
    private readonly HashSet<string> m_projectPaths;
    private readonly Dictionary<Guid, ResourceLicenseInfo> m_licenses;
    private readonly IEnumerator<string> m_files;
    private readonly List<ResourceAuditItem> m_items = [];
    private readonly List<PublishIssue> m_issues = [];
    private readonly byte[] m_buffer = new byte[128 * 1024];
    private FileStream? m_stream;
    private IncrementalHash? m_hash;
    private ResourceAuditItem? m_pending;
    private string m_file = string.Empty;
    private DateTime m_modified;
    /// <summary>Creates an audit over the selected project's own Assets and a previously collected native dependency closure.</summary>
    /// <param name="projectDirectory">The current native editor project directory.</param>
    /// <param name="closure">The existing publication preflight snapshot; no asset database is built here.</param>
    /// <param name="licenses">Optional author-owned native provenance records.</param>
    public ProjectResourceAudit(
        string projectDirectory,
        NovelPublishReport closure,
        ResourceLicenseCatalogAsset? licenses = null
    ) {
        m_sourceRoot = Path.GetFullPath(Path.Combine(projectDirectory, "Assets"));
        if (!Directory.Exists(m_sourceRoot) || File.GetAttributes(m_sourceRoot).HasFlag(FileAttributes.ReparsePoint)) throw new ArgumentException("Audit Assets must be an existing non-reparse project directory.", nameof(projectDirectory));
        m_closure = closure; m_closureComplete = closure.contentValid;
        m_referenced = closure.assets.ToDictionary(static item => item.id);
        m_projectPaths = closure.assets.Where(static item => item.path.source == AssetSourceId.project).Select(static item => item.path.localPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (licenses is not null) ResourceLicenseEditService.Validate(licenses);
        m_licenses = (licenses?.entries ?? []).ToDictionary(static item => item.asset!.identity.persistentId, static item => new ResourceLicenseInfo(item.asset!.identity.persistentId,
            item.author, item.source, item.license, item.redistributionConfirmed, item.attributionRequired, item.notice, item.modifications));
        m_files = Directory.EnumerateFiles(m_sourceRoot, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.Hidden | FileAttributes.System }).GetEnumerator();
        if (!m_closureComplete) m_issues.Add(new(PublishIssueLevel.Pending, "VN-AUDIT-CLOSURE", closure.title, "原生内容检查存在错误；未出现于闭包的资源暂不能判定为未引用。"));
    }
    /// <summary>Gets the number of directory entries visited, including excluded sources.</summary>
    public int visited { get; private set; }
    /// <summary>Gets whether enumeration ended; callers must inspect the report for errors and limits.</summary>
    public bool completed { get; private set; }
    /// <summary>Gets whether the owner cancelled while retaining completed observations.</summary>
    public bool cancelled { get; private set; }
    /// <summary>Gets the relative native source currently being hashed, absent between files.</summary>
    public AssetPath? currentSource => m_pending?.path;
    /// <summary>Gets immutable completed source observations.</summary>
    public IReadOnlyList<ResourceAuditItem> items => m_items.AsReadOnly();
    /// <summary>Visits directory entries or hashes 128 KiB chunks without changing any source resource.</summary>
    /// <param name="budget">One to 256 units; each is one source probe or one bounded hash read.</param>
    public void Step(int budget = 8)
    {
        if (budget is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(budget));
        for (int index = 0; index < budget && !completed && !cancelled; index++)
        {
            try
            {
                if (m_stream is not null) { HashChunk(); continue; }
                if (!m_files.MoveNext()) { Finish(); break; }
                visited++;
                string relative = Path.GetRelativePath(m_sourceRoot, m_files.Current).Replace('\\', '/');
                if (ResourceInventoryRules.IsExcluded(relative) || !ResourceInventoryRules.IsKnown(relative) && !m_projectPaths.Contains(relative)) continue;
                if (m_items.Count >= 100000) { m_issues.Add(new(PublishIssueLevel.Error, "VN-AUDIT-BUDGET", m_sourceRoot, "资源候选达到 100000 项，报告不完整。")); Finish(); break; }
                Probe(m_files.Current, AssetPath.Project(relative));
            }
            catch (Exception failure) when (failure is not OutOfMemoryException)
            {
                m_issues.Add(new(PublishIssueLevel.Error, "VN-AUDIT-READ", m_file.Length > 0 ? m_file : m_sourceRoot, failure.Message));
                if (m_pending is not null) m_items.Add(m_pending with { use = ResourceAuditUse.Unknown, message = failure.Message });
                else { Finish(); break; }
                CloseFile();
            }
        }
    }
    /// <summary>Captures an immutable report; partial and cancelled scans never become completed proofs.</summary>
    /// <returns>A frozen audit with source hashes, direct referrers and author-supplied notices.</returns>
    public ResourceAuditReport Capture() => new(m_closure.title, m_closure.fingerprint, completed, cancelled, m_closureComplete, m_items, m_issues,
        m_closure.assets.Where(static item => item.path.source != AssetSourceId.project), m_licenses.Values.Where(license => m_referenced.ContainsKey(license.assetId)));
    /// <summary>Cancels and releases file/hash/enumeration ownership, preserving completed rows.</summary>
    public void Cancel() { if (completed || cancelled) return; cancelled = true; CloseFile(); m_files.Dispose(); }
    /// <inheritdoc />
    public void Dispose() => Cancel();
    private void Probe(
        string file,
        AssetPath path
    ) {
        m_file = file; m_pending = new(Guid.Empty, path, 0, "", ResourceAuditUse.Unknown, [], null, "");
        var info = new FileInfo(file); long bytes = info.Length; m_modified = info.LastWriteTimeUtc;
        Guid id = Guid.Empty; string message = string.Empty;
        try { if (Assets.TryLoad<AssetObject>(path, out var asset) && asset is not { isMissing: true } && asset is not null) id = asset.identity.persistentId; }
        catch (Exception failure) when (failure is not OutOfMemoryException) { message = failure.Message; }
        if (id == Guid.Empty) message = "正式资产 API 尚未提供可用导入身份；只检查源文件，不判定未引用。 " + message;
        var use = ResourceInventoryRules.IsAuthoring(path.localPath) ? ResourceAuditUse.AuthoringOnly : id == Guid.Empty ? ResourceAuditUse.Unknown
            : m_referenced.ContainsKey(id) ? ResourceAuditUse.Referenced : m_closureComplete ? ResourceAuditUse.NotReferenced : ResourceAuditUse.Unknown;
        var parents = m_referenced.TryGetValue(id, out var included) ? included.referrers : Array.Empty<string>();
        m_pending = new(id, path, bytes, "", use, parents, m_licenses.GetValueOrDefault(id), message);
        m_hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        m_stream = new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, m_buffer.Length, FileOptions.SequentialScan);
    }
    private void HashChunk()
    {
        int count = m_stream!.Read(m_buffer, 0, m_buffer.Length);
        if (count > 0) { m_hash!.AppendData(m_buffer, 0, count); return; }
        var info = new FileInfo(m_file);
        bool changed = !info.Exists || info.Length != m_pending!.bytes || info.LastWriteTimeUtc != m_modified;
        var item = m_pending! with { hash = changed ? "" : Convert.ToHexString(m_hash!.GetHashAndReset()), use = changed ? ResourceAuditUse.Unknown : m_pending.use,
            message = changed ? "扫描期间源文件发生变化；重新检查后才能使用哈希及引用结论。" : m_pending.message };
        m_items.Add(item);
        if (changed) m_issues.Add(new(PublishIssueLevel.Pending, "VN-AUDIT-CHANGED", item.path.ToString(), item.message));
        if (item.use == ResourceAuditUse.Referenced && m_referenced[item.id].path.source == AssetSourceId.project)
        {
            if (item.license is null || item.license.author.Length == 0 || item.license.source.Length == 0 || item.license.license.Length == 0)
                m_issues.Add(new(PublishIssueLevel.Warning, "VN-AUDIT-LICENSE", item.path.ToString(), "资源来源、作者或许可尚未完整记录。"));
            if (item.license is { redistributionConfirmed: false }) m_issues.Add(new(PublishIssueLevel.Pending, "VN-AUDIT-REDISTRIBUTION", item.path.ToString(), "作者尚未确认计划中的游戏分发权限。"));
            if (item.license is { attributionRequired: true, notice.Length: 0 }) m_issues.Add(new(PublishIssueLevel.Warning, "VN-AUDIT-ATTRIBUTION", item.path.ToString(), "记录要求署名，但尚未提供署名文本。"));
        }
        CloseFile();
    }
    private void Finish() { if (completed) return; completed = true; CloseFile(); m_files.Dispose(); }
    private void CloseFile() { m_stream?.Dispose(); m_hash?.Dispose(); m_stream = null; m_hash = null; m_pending = null; m_file = ""; }
}

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using InnoEngine.Assets;
using InnoEditor.Assets;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Authoring.Diagnostics;

namespace Inno.VisualNovel.Authoring.Preview;

/// <summary>Owns one cancellable native preview projection; host asset and document operations stay on the caller's editor thread.</summary>
public sealed class PreviewProjectPreparation : IDisposable
{
    private IEnumerator<bool>? m_steps;
    private FormattableString m_progress = $"准备资源";
    internal PreviewProjectPreparation(
        NovelProjectAsset context,
        StoryDocumentsModule stories,
        ResourceDocumentsModule resources,
        VariableTableAsset? activeVariables
    ) {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(stories); ArgumentNullException.ThrowIfNull(resources);
        m_steps = Prepare(context, stories, resources, activeVariables, context.contentVersion, context.assetPath.ToString()).GetEnumerator();
    }
    /// <summary>Gets whether the candidate is still being prepared.</summary>
    public bool running => m_steps is not null;
    /// <summary>Gets whether the author or lifecycle owner cancelled before completion.</summary>
    public bool cancelled { get; private set; }
    /// <summary>Gets the candidate only after every captured source and document has been checked.</summary>
    public PreviewProjectSnapshot? result { get; private set; }
    /// <summary>Gets an actionable failure; no partially prepared project is exposed.</summary>
    public string error { get; private set; } = string.Empty;
    /// <summary>Gets the owner-local cause for presentation before formatting; it is never part of a save or reload payload.</summary>
    public Exception? failure { get; private set; }
    /// <summary>Gets the current phase and source path without inventing a percentage.</summary>
    public string progress => FormattableString.Invariant(m_progress);
    /// <summary>Gets an independent source template and arguments for the current editor language without changing preparation state.</summary>
    public FormattableString progressMessage => FormattableStringFactory.Create(m_progress.Format, m_progress.GetArguments().ToArray());
    /// <summary>Gets the number of distinct sources captured so far.</summary>
    public int capturedSources { get; private set; }
    /// <summary>Executes bounded preparation steps and yields between native resource operations.</summary>
    /// <param name="stepBudget">Maximum phase boundaries processed in this call, from one to 1024.</param>
    /// <param name="maximumMilliseconds">Wall-clock yield target; an indivisible native capture or chapter compile can exceed it.</param>
    /// <returns>Whether the operation completed, failed or was cancelled.</returns>
    public bool Advance(
        int stepBudget = 8,
        double maximumMilliseconds = 8
    ) {
        if (stepBudget is < 1 or > 1024) throw new ArgumentOutOfRangeException(nameof(stepBudget));
        if (!double.IsFinite(maximumMilliseconds) || maximumMilliseconds <= 0 || maximumMilliseconds > 1000) throw new ArgumentOutOfRangeException(nameof(maximumMilliseconds));
        if (m_steps is null) return true;
        var watch = Stopwatch.StartNew();
        try
        {
            for (int index = 0; index < stepBudget; index++)
            {
                if (!m_steps.MoveNext()) { Release(); return true; }
                if (watch.Elapsed.TotalMilliseconds >= maximumMilliseconds) break;
            }
        }
        catch (Exception cause) when (cause is not OutOfMemoryException)
        { failure = cause; error = cause.Message; result = null; Release(); return true; }
        return false;
    }
    /// <summary>Cancels unfinished work and releases captured generation state without touching an existing preview.</summary>
    public void Cancel()
    {
        if (!running) return;
        cancelled = true; m_progress = $"已取消预览准备"; Release();
    }
    /// <inheritdoc />
    public void Dispose() => Cancel();
    private void Release() { m_steps?.Dispose(); m_steps = null; }

    private IEnumerable<bool> Prepare(
        NovelProjectAsset context,
        StoryDocumentsModule stories,
        ResourceDocumentsModule resources,
        VariableTableAsset? activeVariables,
        long contextVersion,
        string contextPath
    ) {
        var copies = new Dictionary<Guid, AssetObject>(); var stamps = new Dictionary<Guid, PreviewDraftSource>();
        var documents = new Dictionary<Guid, NativeDocumentStamp?>(); var hashes = new SortedDictionary<Guid, byte[]>();
        long bytes = 0; int depth = 0;
        if (context.isMissing || context.contentVersion != contextVersion || context.assetPath.ToString() != contextPath)
            throw new AuthoringValidationException($"预览上下文在准备前已重新导入、移动或缺失，请重新运行。");
        foreach (bool step in Resolve(context)) yield return step;
        if (activeVariables is not null) foreach (bool step in Resolve(activeVariables)) yield return step;
        var project = copies[context.identity.persistentId] as NovelProjectAsset ?? throw new InvalidOperationException("Preview context is not a native VN game definition.");
        m_progress = $"核对资源与草稿 · {stamps.Count}"; yield return true;
        // The final validation and publication share one editor-thread step so an edit cannot slip between them.
        foreach (var captured in stamps.Values)
        {
            var current = Assets.Load<AssetObject>(captured.assetId);
            if (current.isMissing || current.contentVersion != captured.contentVersion || current.assetPath.ToString() != captured.path || !SameDocument(documents[captured.assetId], Stamp(AssetPath.Parse(captured.path))))
                throw new AuthoringValidationException($"预览准备期间资源或草稿已变化，请重新运行：{captured.path}");
        }
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(context.identity.persistentId.ToByteArray());
        foreach (var pair in hashes) { hash.AppendData(pair.Key.ToByteArray()); hash.AppendData(pair.Value); }
        project.buildFingerprint = Convert.ToHexString(hash.GetHashAndReset());
        result = new(project, stamps.Values.OrderBy(static source => source.path, StringComparer.Ordinal), copies.Where(static item => item.Value is VariableTableAsset)
            .ToDictionary(static item => item.Key, static item => (VariableTableAsset)item.Value));
        m_progress = $"预览资源准备完成";

        NativeDocumentStamp? Stamp(AssetPath path) => stories.GetStamp(path) ?? resources.GetStamp(path);
        IEnumerable<bool> Resolve(AssetObject asset)
        {
            ArgumentNullException.ThrowIfNull(asset);
            if (copies.ContainsKey(asset.identity.persistentId)) yield break;
            if (++depth > 128) throw new InvalidOperationException("Preview resource closure exceeds its 128-level dependency depth.");
            try
            {
                Guid id = asset.identity.persistentId;
                if (asset.isMissing || id == Guid.Empty || asset.assetPath.localPath.Length == 0) throw new InvalidOperationException("Preview requires an available persistent native resource: " + asset.assetPath);
                if (copies.Count >= 10000) throw new InvalidOperationException("Preview resource closure exceeds its 10000-asset budget.");
                m_progress = $"捕获资源 · {asset.assetPath}"; yield return true;
                var before = Stamp(asset.assetPath); bool dirty; AssetObject value;
                if (asset is StoryAsset && stories.TryReadUnsaved(asset.assetPath, out var chapter)) { value = chapter!.ToAsset(); dirty = true; }
                else if (resources.TryReadUnsaved(asset.assetPath, out var draft)) { value = draft!; dirty = true; }
                else { value = asset; dirty = false; }
                AssetObject copy = value switch { NovelProjectAsset game => NovelProjectCopy.Copy(game), ThemeAsset theme => ThemeAssetCopy.Copy(theme), _ => value };
                copies.Add(id, copy); stamps.Add(id, new(id, asset.assetPath.ToString(), asset.contentVersion, dirty)); documents.Add(id, before); capturedSources = copies.Count;
                var properties = EditorAssets.CaptureProperties(value); bytes = checked(bytes + properties.data.Length);
                if (bytes > 1024L * 1024 * 1024) throw new InvalidOperationException("Preview metadata exceeds its 1 GiB preparation budget.");
                byte[] version = new byte[8]; BinaryPrimitives.WriteInt64LittleEndian(version, asset.contentVersion);
                using (var itemHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
                { itemHash.AppendData(version); itemHash.AppendData(properties.data.Span); hashes.Add(id, itemHash.GetHashAndReset()); }
                yield return true;
                foreach (var dependency in properties.dependencies)
                    foreach (bool step in Resolve(Assets.Load<AssetObject>(dependency.persistentId))) yield return step;
                if (value is StoryAsset story)
                {
                    VariableTableAsset? table = story.variableTable is null ? null : ResolveTyped(story.variableTable);
                    bool tableDirty = story.variableTable is not null && stamps[story.variableTable.identity.persistentId].unsaved;
                    if (dirty || tableDirty)
                    {
                        m_progress = $"编译章节 · {story.chapterId}"; yield return true;
                        StoryAsset source = dirty ? story : stories.Inspect(asset.assetPath).draft.ToAsset();
                        source.variableTable = table; copy = PreviewContentBuilder.Compile(source, stories.compiler); copies[id] = copy;
                    }
                }
                if (copy is NovelProjectAsset gameResult)
                {
                    gameResult.chapters = gameResult.chapters.Select(ResolveTyped).ToArray();
                    gameResult.stages = gameResult.stages.Select(ResolveTyped).ToArray(); gameResult.characters = gameResult.characters.Select(ResolveTyped).ToArray();
                    gameResult.audio = gameResult.audio.Select(ResolveTyped).ToArray(); gameResult.performances = gameResult.performances.Select(ResolveTyped).ToArray();
                    gameResult.particles = gameResult.particles.Select(ResolveTyped).ToArray(); gameResult.screenEffects = gameResult.screenEffects.Select(ResolveTyped).ToArray();
                    gameResult.transitions = gameResult.transitions.Select(ResolveTyped).ToArray(); gameResult.themes = gameResult.themes.Select(ResolveTyped).ToArray();
                    gameResult.screens = gameResult.screens.Select(binding => new NovelScreenBinding { role = binding.role, screen = binding.screen is null ? null : ResolveTyped(binding.screen) }).ToArray();
                    gameResult.localization = gameResult.localization is null ? null : ResolveTyped(gameResult.localization);
                    gameResult.collections = gameResult.collections is null ? null : ResolveTyped(gameResult.collections);
                    gameResult.additionalResources = gameResult.additionalResources.Select(ResolveValue).ToArray();
                }
                else if (copy is ThemeAsset themeResult)
                    themeResult.screens = themeResult.screens.Select(binding => new NovelScreenBinding { role = binding.role, screen = binding.screen is null ? null : ResolveTyped(binding.screen) }).ToArray();
                yield return true;
            }
            finally { depth--; }
        }
        AssetObject ResolveValue(AssetObject asset) => copies.TryGetValue(asset.identity.persistentId, out var copy) ? copy
            : throw new InvalidOperationException("The native source capture omitted a referenced preview resource: " + asset.assetPath);
        T ResolveTyped<T>(T asset) where T : AssetObject => ResolveValue(asset) as T
            ?? throw new InvalidOperationException("The open preview document changed its native resource type: " + asset.assetPath);
    }
    private static bool SameDocument(
        NativeDocumentStamp? before,
        NativeDocumentStamp? after
    ) => before?.documentId == after?.documentId ? before == after : before?.unsaved != true && after?.unsaved != true;
}

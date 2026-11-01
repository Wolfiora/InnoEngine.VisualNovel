using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Assets;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Authoring.Diagnostics;

namespace Inno.VisualNovel.Authoring.BuiltInResources;

/// <summary>Copies a reviewed preset selection one native root at a time, retaining explicit outputs on cancellation or failure.</summary>
public sealed class BuiltInResourceCopyBatch
{
    private sealed record Source(Guid id, string path, long revision);
    private sealed record Input(Source root, Source[] pages);
    private readonly Input[] m_inputs;
    private readonly List<BuiltInResourceCopyResult> m_created = [];
    private readonly List<BuiltInResourceCopyResult> m_roots = [];
    /// <summary>Freezes available native identities before any project source is written.</summary>
    /// <param name="sources">One to 256 distinct editable presets; immutable image pixels are excluded.</param>
    public BuiltInResourceCopyBatch(IEnumerable<AssetObject> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var values = sources.Take(257).ToArray();
        if (values.Length is < 1 or > 256 || values.Any(static source => source is null || source.isMissing || source.identity.persistentId == Guid.Empty
            || source.assetPath.localPath.Length == 0 || source is not (StageAsset or CharacterAsset or PerformanceClipAsset or VisualEffectAsset or ScreenAsset or ThemeAsset or AudioCueAsset))
            || values.Select(static source => source.identity.persistentId).Distinct().Count() != values.Length)
            throw new AuthoringArgumentException($"批量复制需要 1～256 个不同且可用的可编辑原生资源；图标作为不可变图片引用使用。", nameof(sources));
        m_inputs = values.Select(source => new Input(Capture(source), source is ThemeAsset theme
            ? theme.screens.Select(binding => Capture(binding.screen ?? throw new InvalidOperationException("A selected theme page is unassigned.")))
                .DistinctBy(static page => page.id).ToArray() : [])).ToArray();
    }
    /// <summary>Gets the number of requested roots, excluding theme-dependent page outputs.</summary>
    public int total => m_inputs.Length;
    /// <summary>Gets the number of fully committed requested roots.</summary>
    public int completed => m_roots.Count;
    /// <summary>Gets whether another root can be processed by the owner on an editor update.</summary>
    public bool running => !cancelled && error.Length == 0 && completed < total;
    /// <summary>Gets whether the author or lifecycle owner stopped before all roots were committed.</summary>
    public bool cancelled { get; private set; }
    /// <summary>Gets the first failure; previously committed outputs remain available and are never silently deleted.</summary>
    public string error { get; private set; } = string.Empty;
    /// <summary>Gets the transient owner-local cause, retaining authoring format arguments until presentation.</summary>
    public Exception? failure { get; private set; }
    /// <summary>Gets all native outputs, including theme pages committed before a later failure.</summary>
    public IReadOnlyList<BuiltInResourceCopyResult> created => m_created.AsReadOnly();
    /// <summary>Gets successfully completed roots in the explicit selection order.</summary>
    public IReadOnlyList<BuiltInResourceCopyResult> roots => m_roots.AsReadOnly();
    /// <summary>Copies at most one requested root through the same native copy service used by individual presets.</summary>
    /// <returns>Whether the batch has completed, failed or been cancelled.</returns>
    public bool Advance()
    {
        if (!running) return true;
        var input = m_inputs[completed];
        try
        {
            var source = Resolve(input.root);
            foreach (var page in input.pages) _ = Resolve(page);
            var copy = BuiltInResourceCopyService.Copy(source, value => m_created.Add(new(input.root.path, value.assetPath.ToString(), value.identity.persistentId)));
            m_roots.Add(new(input.root.path, copy.assetPath.ToString(), copy.identity.persistentId));
        }
        catch (Exception cause) when (cause is not OutOfMemoryException) { failure = cause; error = cause.Message; }
        return !running;
    }
    /// <summary>Stops before the next root; already committed assets remain listed for review and reuse.</summary>
    public void Cancel() { if (running) cancelled = true; }
    private static Source Capture(AssetObject asset)
    {
        if (asset.isMissing || asset.identity.persistentId == Guid.Empty || asset.assetPath.localPath.Length == 0)
            throw new InvalidOperationException("A selected resource or owned theme page has no available native identity.");
        return new(asset.identity.persistentId, asset.assetPath.ToString(), asset.contentVersion);
    }
    private static AssetObject Resolve(Source source)
    {
        var asset = Assets.Load<AssetObject>(source.id);
        if (asset.isMissing || asset.assetPath.ToString() != source.path || asset.contentVersion != source.revision)
            throw new AuthoringValidationException($"源资源在批量准备后已改变、移动或缺失，请重新选择：{source.path}");
        return asset;
    }
}

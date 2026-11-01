using System;
using System.Collections.Generic;
using InnoEditor.Assets;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Stage.Timeline;

namespace Inno.VisualNovel.Authoring.AssetImporting;

/// <summary>Validates native reusable performances before publishing runtime content.</summary>
[AssetImporter("inno.vn.performance")]
public sealed class PerformanceClipImporter : NativeNovelAssetImporter<PerformanceClipAsset>
{
    /// <inheritdoc />
    public override IReadOnlyList<string> supportedExtensions { get; } = [".ivnclip"];
    /// <inheritdoc />
    protected override void Validate(PerformanceClipAsset asset)
    {
        if (string.IsNullOrWhiteSpace(asset.resourceId)) throw new InvalidOperationException("A performance requires a stable resource key.");
        _ = new PerformancePlayer(asset);
    }
}

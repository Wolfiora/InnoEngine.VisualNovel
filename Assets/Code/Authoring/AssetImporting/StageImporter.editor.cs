using System;
using System.Collections.Generic;
using System.Linq;
using InnoEditor.Assets;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Stage.Composition;

namespace Inno.VisualNovel.Authoring.AssetImporting;

/// <summary>Validates layered scenery through the same composition rules used by games.</summary>
[AssetImporter("inno.vn.stage")]
public sealed class StageImporter : NativeNovelAssetImporter<StageAsset>
{
    /// <inheritdoc />
    public override IReadOnlyList<string> supportedExtensions { get; } = [".ivnstage"];
    /// <inheritdoc />
    protected override void Validate(StageAsset asset)
    {
        if (string.IsNullOrWhiteSpace(asset.resourceId) || asset.layers.Select(static layer => layer.id).Distinct(StringComparer.Ordinal).Count() != asset.layers.Length
            || !double.IsFinite(asset.width) || !double.IsFinite(asset.height) || asset.width <= 0 || asset.height <= 0
            || asset.layers.Any(layer => string.IsNullOrWhiteSpace(layer.id) || layer.texture < -1 || layer.texture >= asset.textures.Length))
            throw new InvalidOperationException("Scenery requires a stable key, valid dimensions, unique layers and assigned image slots.");
        new StageController(asset.textures).SetStage(asset);
    }
}

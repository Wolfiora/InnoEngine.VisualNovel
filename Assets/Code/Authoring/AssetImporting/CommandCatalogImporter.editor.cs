using System;
using System.Collections.Generic;
using System.Linq;
using InnoEditor.Assets;
using Inno.VisualNovel.Model.Extensions;

namespace Inno.VisualNovel.Authoring.AssetImporting;

/// <summary>Validates published neutral extension forms through the native asset pipeline.</summary>
[AssetImporter("inno.vn.commands")]
public sealed class CommandCatalogImporter : NativeNovelAssetImporter<NovelCommandCatalogAsset>
{
    /// <inheritdoc />
    public override IReadOnlyList<string> supportedExtensions { get; } = [".ivncommands"];
    /// <inheritdoc />
    protected override void Validate(NovelCommandCatalogAsset asset)
    {
        if (asset.definitions.Length > 1024 || asset.definitions.Select(static definition => definition.id).Distinct(StringComparer.Ordinal).Count() != asset.definitions.Length)
            throw new InvalidOperationException("Command catalog requires at most 1024 unique contracts.");
        foreach (var definition in asset.definitions) CommandContract.Validate(definition);
    }
}

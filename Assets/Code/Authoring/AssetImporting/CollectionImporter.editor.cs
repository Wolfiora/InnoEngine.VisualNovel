using System.Collections.Generic;
using InnoEditor.Assets;
using Inno.VisualNovel.Model.Collections;
using Inno.VisualNovel.Player.Collections;

namespace Inno.VisualNovel.Authoring.AssetImporting;

/// <summary>Imports native collection metadata while retaining stable unlock identities and dependencies.</summary>
[AssetImporter("inno.vn.collection")]
public sealed class CollectionImporter : NativeNovelAssetImporter<CollectionAsset>
{
    /// <inheritdoc />
    public override IReadOnlyList<string> supportedExtensions { get; } = [".ivncollection"];
    /// <inheritdoc />
    protected override void Validate(CollectionAsset asset) => _ = new NovelCollectionService(asset);
}

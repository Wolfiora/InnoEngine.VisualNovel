using System.Collections.Generic;
using InnoEditor.Assets;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Authoring.AssetImporting;

/// <summary>Imports shared declarations through the same native source and artifact pipeline as other VN assets.</summary>
[AssetImporter("inno.vn.variables")]
public sealed class VariableTableImporter : NativeNovelAssetImporter<VariableTableAsset>
{
    /// <inheritdoc />
    public override IReadOnlyList<string> supportedExtensions { get; } = [".ivnvariables"];
    /// <inheritdoc />
    protected override void Validate(VariableTableAsset asset) => StoryVariableDeclarations.Validate(asset);
}

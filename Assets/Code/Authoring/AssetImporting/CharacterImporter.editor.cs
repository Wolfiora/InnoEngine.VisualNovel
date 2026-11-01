using System;
using System.Collections.Generic;
using System.Linq;
using InnoEditor.Assets;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Stage.Composition;

namespace Inno.VisualNovel.Authoring.AssetImporting;

/// <summary>Rejects missing character parts and invalid pose/expression combinations before publishing.</summary>
[AssetImporter("inno.vn.character")]
public sealed class CharacterImporter : NativeNovelAssetImporter<CharacterAsset>
{
    /// <inheritdoc />
    public override IReadOnlyList<string> supportedExtensions { get; } = [".ivncharacter"];
    /// <inheritdoc />
    protected override void Validate(CharacterAsset asset)
    {
        CharacterDefinitionRules.Validate(asset); CharacterAnimationRules.Validate(asset);
        if (string.IsNullOrWhiteSpace(asset.resourceId) || asset.poses.Length == 0 || !asset.poses.Any(pose => pose.id == asset.defaultPose)
            || asset.poses.Select(static pose => pose.id).Distinct(StringComparer.Ordinal).Count() != asset.poses.Length
            || asset.expressions.Select(static expression => expression.id).Distinct(StringComparer.Ordinal).Count() != asset.expressions.Length
            || asset.expressions.Length > 0 && !asset.expressions.Any(expression => expression.id == asset.defaultExpression))
            throw new InvalidOperationException("A character needs a stable key and unique poses/expressions with valid defaults.");
        var stage = new StageController(asset.textures);
        foreach (CharacterPose pose in asset.poses)
        {
            if (string.IsNullOrWhiteSpace(pose.id) || pose.parts.Select(static part => part.id).Distinct(StringComparer.Ordinal).Count() != pose.parts.Length
                || pose.parts.Any(part => string.IsNullOrWhiteSpace(part.id) || part.texture < 0 || part.texture >= asset.textures.Length))
                throw new InvalidOperationException("Character pose has duplicate parts or an unassigned image: " + pose.id);
            if (asset.expressions.Length == 0) stage.ShowCharacter("validation", asset, pose.id, "", new());
            foreach (CharacterExpression expression in asset.expressions)
            {
                if (string.IsNullOrWhiteSpace(expression.id) || expression.parts.Select(static part => part.partId).Distinct(StringComparer.Ordinal).Count() != expression.parts.Length
                    || expression.parts.Any(part => part.texture < -1 || part.texture >= asset.textures.Length || part.visibility is < -1 or > 1))
                    throw new InvalidOperationException("Character expression has invalid overrides: " + expression.id);
                stage.ShowCharacter("validation", asset, pose.id, expression.id, new());
            }
        }
    }
}

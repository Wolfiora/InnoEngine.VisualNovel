using System.Linq;

namespace Inno.VisualNovel.Model.Characters;

/// <summary>Freezes layered character declarations independently of imported image residency.</summary>
public static class CharacterAssetCopy
{
    /// <summary>Copies all poses, expressions and part values while borrowing image identities.</summary>
    /// <param name="source">The imported layered character.</param>
    /// <returns>A detached generation-local declaration.</returns>
    public static CharacterAsset Copy(CharacterAsset source) => new()
    {
        resourceId = source.resourceId, displayName = source.displayName, defaultPose = source.defaultPose, defaultExpression = source.defaultExpression,
        aliases = source.aliases.ToArray(), nameColor = source.nameColor, portrait = source.portrait,
        breathing = source.breathing.Copy(), textures = source.textures.ToArray(), poses = source.poses.Select(static pose => pose.Copy()).ToArray(),
        expressions = source.expressions.Select(static expression => expression.Copy()).ToArray()
    };

    /// <summary>Replaces authored values with a detached copy while preserving the destination's native asset identity.</summary>
    /// <param name="source">The complete authored replacement.</param>
    /// <param name="destination">The existing candidate whose native identity must be retained.</param>
    public static void CopyInto(
        CharacterAsset source,
        CharacterAsset destination
    ) {
        CharacterAsset copy = Copy(source);
        destination.resourceId = copy.resourceId; destination.displayName = copy.displayName; destination.defaultPose = copy.defaultPose; destination.defaultExpression = copy.defaultExpression;
        destination.aliases = copy.aliases; destination.nameColor = copy.nameColor; destination.portrait = copy.portrait;
        destination.breathing = copy.breathing; destination.textures = copy.textures; destination.poses = copy.poses; destination.expressions = copy.expressions;
    }
}

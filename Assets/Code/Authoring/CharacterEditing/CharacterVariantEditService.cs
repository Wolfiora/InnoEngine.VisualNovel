using System;
using System.Linq;
using Inno.VisualNovel.Model.Characters;

namespace Inno.VisualNovel.Authoring.CharacterEditing;

/// <summary>Owns stable pose/expression membership and explicit replacement of deleted defaults.</summary>
public static class CharacterVariantEditService
{
    /// <summary>Creates a fresh expression inheriting the chosen pose without overriding its parts.</summary>
    /// <param name="character">The detached character candidate.</param>
    /// <returns>The new stable expression identity.</returns>
    public static string AddExpression(CharacterAsset character)
    {
        if (character.expressions.Length >= 1024) throw new InvalidOperationException("A character supports at most 1024 expressions.");
        string id = Guid.NewGuid().ToString("N");
        character.expressions = [.. character.expressions, new() { id = id, name = "新表情" }];
        if (character.expressions.Length == 1) character.defaultExpression = id;
        return id;
    }

    /// <summary>Duplicates all overrides with a fresh identity and no mutable aliases.</summary>
    /// <param name="character">The detached character candidate.</param>
    /// <param name="expressionId">The exact expression to duplicate.</param>
    /// <returns>The new stable expression identity.</returns>
    public static string DuplicateExpression(
        CharacterAsset character,
        string expressionId
    ) {
        if (character.expressions.Length >= 1024) throw new InvalidOperationException("A character supports at most 1024 expressions.");
        var copy = character.expressions.Single(item => item.id == expressionId).Copy();
        copy.id = Guid.NewGuid().ToString("N"); copy.name += " 副本";
        character.expressions = [.. character.expressions, copy]; return copy.id;
    }

    /// <summary>Deletes a pose only after validating the remaining explicitly chosen default.</summary>
    /// <param name="character">The detached character candidate.</param>
    /// <param name="poseId">The exact pose to remove.</param>
    /// <param name="replacementDefault">A remaining pose required when deleting the current default.</param>
    public static void RemovePose(
        CharacterAsset character,
        string poseId,
        string replacementDefault = ""
    ) {
        _ = character.poses.Single(item => item.id == poseId);
        if (character.poses.Length <= 1) throw new InvalidOperationException("Keep at least one usable character pose.");
        string next = character.defaultPose;
        if (next == poseId)
        {
            if (!character.poses.Any(item => item.id != poseId && item.id == replacementDefault))
                throw new InvalidOperationException("Choose a remaining default pose before deleting this pose.");
            next = replacementDefault;
        }
        character.poses = character.poses.Where(item => item.id != poseId).ToArray(); character.defaultPose = next;
    }

    /// <summary>Deletes an expression without guessing a replacement default or removing any part images.</summary>
    /// <param name="character">The detached character candidate.</param>
    /// <param name="expressionId">The exact expression to remove.</param>
    /// <param name="replacementDefault">A remaining expression required when deleting the current default.</param>
    public static void RemoveExpression(
        CharacterAsset character,
        string expressionId,
        string replacementDefault = ""
    ) {
        _ = character.expressions.Single(item => item.id == expressionId);
        if (character.expressions.Length <= 1) throw new InvalidOperationException("Keep at least one expression when editing this expression library.");
        string next = character.defaultExpression;
        if (next == expressionId)
        {
            if (!character.expressions.Any(item => item.id != expressionId && item.id == replacementDefault))
                throw new InvalidOperationException("Choose a remaining default expression before deleting this expression.");
            next = replacementDefault;
        }
        character.expressions = character.expressions.Where(item => item.id != expressionId).ToArray(); character.defaultExpression = next;
    }

    /// <summary>Restores one part to inherited pose values while retaining unrelated expression overrides.</summary>
    /// <param name="character">The detached character candidate.</param>
    /// <param name="expressionId">The exact expression whose override is cleared.</param>
    /// <param name="partId">The semantic part shared by poses.</param>
    public static void ClearPartOverride(
        CharacterAsset character,
        string expressionId,
        string partId
    ) {
        var expression = character.expressions.Single(item => item.id == expressionId);
        expression.parts = expression.parts.Where(item => item.partId != partId).ToArray();
    }
}

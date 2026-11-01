using System;
using System.Linq;

namespace Inno.VisualNovel.Model.Characters;

/// <summary>Validates bounded character metadata shared by import, publication and sessions.</summary>
public static class CharacterDefinitionRules
{
    /// <summary>Rejects ambiguous aliases, invalid identities and unavailable assigned portraits.</summary>
    /// <param name="character">The authored or frozen character declaration.</param>
    public static void Validate(CharacterAsset character)
    {
        ArgumentNullException.ThrowIfNull(character);
        if (string.IsNullOrWhiteSpace(character.resourceId) || character.resourceId.Length > 128
            || string.IsNullOrWhiteSpace(character.displayName) || character.displayName.Length > 256
            || character.aliases.Length > 64 || character.aliases.Any(static name => string.IsNullOrWhiteSpace(name) || name.Length > 128 || name != name.Trim())
            || character.aliases.Distinct(StringComparer.OrdinalIgnoreCase).Count() != character.aliases.Length)
            throw new InvalidOperationException("Character metadata requires bounded names, a stable identity and unique nonempty aliases: " + character.resourceId);
        if (character.portrait is { isMissing: true }) throw new InvalidOperationException("The assigned character portrait is missing: " + character.resourceId);
    }
}

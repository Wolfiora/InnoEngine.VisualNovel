using System;
using System.Collections.Generic;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Narrative.Reading;

namespace Inno.VisualNovel.Player.Sessions;

internal sealed class NovelSpeakerSource(IReadOnlyDictionary<string, CharacterAsset> characters) : StorySpeakerSource
{
    public override string ResolveName(
        string characterId,
        StoryTextSource? texts
    ) {
        if (!characters.TryGetValue(characterId, out var character)) throw new InvalidOperationException("The linked dialogue character is missing: " + characterId);
        return texts?.Resolve(StoryTextKeys.CharacterName(characterId), character.displayName) ?? character.displayName;
    }
}

using Inno.VisualNovel.Model.Localization;

namespace Inno.VisualNovel.Narrative.Reading;

/// <summary>Resolves frozen character names while keeping asset access outside the interpreter.</summary>
public abstract class StorySpeakerSource
{
    /// <summary>Resolves a declared character's literal default name in the selected language.</summary>
    /// <param name="characterId">The stable linked character identity.</param>
    /// <param name="texts">The current optional game-language view.</param>
    /// <returns>The literal display name; missing characters must produce a descriptive failure.</returns>
    public abstract string ResolveName(
        string characterId,
        StoryTextSource? texts
    );
}

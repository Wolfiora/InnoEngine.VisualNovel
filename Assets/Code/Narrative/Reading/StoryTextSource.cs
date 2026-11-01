namespace Inno.VisualNovel.Narrative.Reading;

/// <summary>Resolves authored text and voice identities without giving the interpreter asset or editor access.</summary>
public abstract class StoryTextSource
{
    /// <summary>Gets the selected game language identity, empty for an untranslated source.</summary>
    public virtual string languageId => string.Empty;
    /// <summary>Resolves one text, retaining source text when no current translation exists.</summary>
    /// <param name="id">The stable table identity.</param>
    /// <param name="source">The current authored source.</param>
    /// <returns>The selected text before variable interpolation.</returns>
    public abstract string Resolve(
        string id,
        string source);
    /// <summary>Resolves an optional localized voice.</summary>
    /// <param name="id">The line's localization identity.</param>
    /// <param name="sourceText">The authored source used to validate translation freshness.</param>
    /// <param name="sourceVoice">The original cue, empty for an unvoiced line.</param>
    /// <returns>The declared cue to play.</returns>
    public virtual string ResolveVoice(
        string id,
        string sourceText,
        string sourceVoice
    ) => sourceVoice;
}

using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Player.Localization;

namespace Inno.VisualNovel.Authoring.LocalizationEditing;

/// <summary>Owns translation edits, source synchronization and review state independently of editor widgets.</summary>
public static class LocalizationEditService
{
    /// <summary>Adds or renames a declared language without discarding translation work.</summary>
    /// <param name="table">The detached native table.</param>
    /// <param name="id">The stable language tag.</param>
    /// <param name="name">The native display name.</param>
    public static void SetLanguage(
        LocalizationTableAsset table,
        string id,
        string name
    ) {
        var language = table.languages.SingleOrDefault(item => item.id == id)?.Copy() ?? new() { id = id };
        language.name = name;
        var languages = table.languages.Where(item => item.id != id).Append(language).ToArray();
        _ = new NovelLocalizationService(new() { sourceLanguage = table.sourceLanguage, languages = languages, entries = table.entries }, table.sourceLanguage);
        table.languages = languages;
    }
    /// <summary>Replaces one complete language presentation declaration after validating its borrowed resources.</summary>
    /// <param name="table">The detached native table.</param>
    /// <param name="language">An existing language with its proposed font and image policy.</param>
    public static void SetPresentation(
        LocalizationTableAsset table,
        LanguageDefinition language
    ) {
        if (!table.languages.Any(item => item.id == language.id)) throw new InvalidOperationException("Select a declared language before editing its presentation.");
        var copy = language.Copy(); _ = new NovelLocalizedResources(copy);
        table.languages = table.languages.Select(item => item.id == copy.id ? copy : item).ToArray();
    }
    /// <summary>Merges current sources while retaining translations and orphan rows for explicit author review.</summary>
    /// <param name="table">The detached destination.</param>
    /// <param name="sources">Uniquely identified current authored sources.</param>
    public static void Synchronize(
        LocalizationTableAsset table,
        IReadOnlyList<LocalizedText> sources
    ) {
        var current = table.entries.ToDictionary(static item => item.id, StringComparer.Ordinal);
        if (sources.Select(static item => item.id).Distinct(StringComparer.Ordinal).Count() != sources.Count) throw new InvalidOperationException("Extracted localization identities are duplicated.");
        foreach (var source in sources)
        {
            if (current.TryGetValue(source.id, out var row)) row.source = source.source;
            else current.Add(source.id, new() { id = source.id, source = source.source });
        }
        table.entries = current.Values.OrderBy(static row => row.id, StringComparer.Ordinal).ToArray();
    }
    /// <summary>Sets an explicitly reviewed translation and records the exact source revision it covers.</summary>
    /// <param name="table">The detached table.</param>
    /// <param name="id">The current text identity.</param>
    /// <param name="language">A declared target language.</param>
    /// <param name="text">The translated string.</param>
    /// <param name="status">Draft, translated or reviewed.</param>
    /// <param name="voiceCueId">Optional localized voice identity.</param>
    public static void SetTranslation(
        LocalizationTableAsset table,
        string id,
        string language,
        string text,
        TranslationStatus status,
        string voiceCueId = ""
    ) {
        if (!table.languages.Any(item => item.id == language) || language == table.sourceLanguage || !Enum.IsDefined(status)) throw new ArgumentException("Select a declared target language and valid translation state.");
        LocalizedText row = table.entries.Single(item => item.id == id);
        if (status != TranslationStatus.Draft) NovelLocalizationService.RequireMatchingPlaceholders(row.source, text);
        row.translations = [.. row.translations.Where(item => item.language != language), new() { language = language, text = text, status = status,
            sourceFingerprint = NovelLocalizationService.Fingerprint(row.source), voiceCueId = voiceCueId }];
    }
    /// <summary>Returns whether a row has a usable current translation.</summary>
    /// <param name="row">The extracted source row.</param>
    /// <param name="language">The target language.</param>
    /// <returns>False for missing, draft or source-modified translations.</returns>
    public static bool IsCurrent(
        LocalizedText row,
        string language
    ) => row.translations.Any(item => item.language == language && item.status != TranslationStatus.Draft && item.sourceFingerprint == NovelLocalizationService.Fingerprint(row.source));
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Model.Text;
using Inno.VisualNovel.Narrative.Reading;

namespace Inno.VisualNovel.Player.Localization;

/// <summary>Owns immutable translation content and a selected language for one game session.</summary>
public sealed class NovelLocalizationService : StoryTextSource
{
    private sealed record Translation(string text, string sourceHash, TranslationStatus status, string voice);
    private sealed record Entry(string source, Dictionary<string, Translation> translations);
    private readonly Dictionary<string, Entry> m_entries;
    private readonly (string id, string name)[] m_languages;
    private readonly string m_sourceLanguage;
    private readonly string m_defaultLanguage;
    private readonly Dictionary<string, NovelLocalizedResources> m_resources;
    /// <summary>Validates and freezes native localization data without retaining editable source objects.</summary>
    /// <param name="table">The optional imported table.</param>
    /// <param name="defaultLanguage">The initial game language, independent of the editor.</param>
    public NovelLocalizationService(
        LocalizationTableAsset? table,
        string defaultLanguage
    ) {
        if (table is { isMissing: true }) throw new InvalidOperationException("The game localization table is missing.");
        m_defaultLanguage = defaultLanguage;
        m_sourceLanguage = table?.sourceLanguage ?? defaultLanguage;
        m_languages = table?.languages.Select(static item => (item.id, item.name)).ToArray() ?? [(defaultLanguage, defaultLanguage)];
        if (m_languages.Length is < 1 or > 128 || m_languages.Any(static item => !ValidTag(item.id) || string.IsNullOrWhiteSpace(item.name))
            || m_languages.Select(static item => item.id).Distinct(StringComparer.Ordinal).Count() != m_languages.Length
            || !m_languages.Any(item => item.id == m_sourceLanguage) || !m_languages.Any(item => item.id == defaultLanguage))
            throw new InvalidOperationException("Localization requires unique named languages, a declared source language and a declared default language.");
        m_resources = (table?.languages ?? [new() { id = defaultLanguage, name = defaultLanguage }]).ToDictionary(static item => item.id, static item => new NovelLocalizedResources(item), StringComparer.Ordinal);
        var entries = table?.entries ?? [];
        if (entries.Length > 1000000) throw new InvalidOperationException("Localization exceeds its one-million-entry budget.");
        m_entries = new(StringComparer.Ordinal);
        foreach (LocalizedText row in entries)
        {
            if (string.IsNullOrWhiteSpace(row.id) || row.id.Length > 4096 || row.translations.Length > m_languages.Length)
                throw new InvalidOperationException("Localization rows require bounded stable identities and declared translations.");
            var translations = new Dictionary<string, Translation>(StringComparer.Ordinal);
            foreach (LocalizedTranslation item in row.translations)
            {
                if (!m_languages.Any(language => language.id == item.language) || !Enum.IsDefined(item.status)
                    || !translations.TryAdd(item.language, new(item.text, item.sourceFingerprint, item.status, item.voiceCueId)))
                    throw new InvalidOperationException("Translation languages must be declared and unique per row: " + row.id);
                if (item.status != TranslationStatus.Draft && item.sourceFingerprint == Fingerprint(row.source)) RequireMatchingPlaceholders(row.source, item.text);
            }
            if (!m_entries.TryAdd(row.id, new(row.source, translations))) throw new InvalidOperationException("Duplicate localization identity: " + row.id);
        }
        language = defaultLanguage;
    }
    private NovelLocalizationService(
        NovelLocalizationService source,
        string language
    ) {
        m_entries = source.m_entries; m_languages = source.m_languages; m_sourceLanguage = source.m_sourceLanguage;
        m_defaultLanguage = source.m_defaultLanguage; this.language = language;
        m_resources = source.m_resources;
    }
    /// <summary>Gets the selected language tag.</summary>
    public string language { get; }
    /// <summary>Gets the current language's immutable image and font policy.</summary>
    public NovelLocalizedResources resources => m_resources[language];
    /// <inheritdoc />
    public override string languageId => language;
    /// <summary>Gets immutable language metadata in authored order.</summary>
    public IReadOnlyList<(string id, string name)> languages => Array.AsReadOnly(m_languages);
    /// <summary>Creates a language candidate while sharing only immutable frozen content.</summary>
    /// <param name="id">A declared language, or empty for the game's default.</param>
    /// <returns>A new language view; an unknown language throws before changing the game.</returns>
    public NovelLocalizationService ForLanguage(string id)
    {
        id = id.Length == 0 ? m_defaultLanguage : id;
        if (!m_languages.Any(item => item.id == id)) throw new ArgumentException("Game language is unavailable: " + id, nameof(id));
        return new(this, id);
    }
    /// <inheritdoc />
    public override string Resolve(
        string id,
        string source
    ) => Find(id, source)?.text ?? source;
    /// <inheritdoc />
    public override string ResolveVoice(
        string id,
        string sourceText,
        string sourceVoice
    ) => Find(id, sourceText) is { voice.Length: > 0 } translation ? translation.voice : sourceVoice;
    /// <summary>Computes a source revision without introducing a separate persistence format.</summary>
    /// <param name="text">The exact authored source.</param>
    /// <returns>An uppercase UTF-8 SHA-256 digest.</returns>
    public static string Fingerprint(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    /// <summary>Rejects translations that remove, add or rename interpolated game variables.</summary>
    /// <param name="source">The authored source.</param>
    /// <param name="translation">The candidate translation.</param>
    public static void RequireMatchingPlaceholders(
        string source,
        string translation
    ) {
        if (!Placeholders(source).SetEquals(Placeholders(translation))) throw new InvalidOperationException("Translation variables must match the source text.");
    }
    private Translation? Find(
        string id,
        string source
    ) => language != m_sourceLanguage && m_entries.TryGetValue(id, out Entry? entry) && entry.source == source
        && entry.translations.TryGetValue(language, out Translation? item) && item.status != TranslationStatus.Draft
        && item.sourceHash == Fingerprint(source) ? item : null;
    private static bool ValidTag(string id) => id.Length is > 0 and <= 64 && id[0] != '-' && id[^1] != '-' && !id.Contains("--", StringComparison.Ordinal)
        && id.All(static ch => char.IsAsciiLetterOrDigit(ch) || ch == '-');
    private static HashSet<string> Placeholders(string text)
    {
        try { return NovelTextTemplate.Parse(text).Where(static token => token.variable).Select(static token => token.value).ToHashSet(StringComparer.Ordinal); }
        catch (FormatException failure) { throw new InvalidOperationException("Invalid localization interpolation.", failure); }
    }
}

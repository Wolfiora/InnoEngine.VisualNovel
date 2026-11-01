using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Player.Localization;
using Inno.VisualNovel.Authoring.AssetImporting;
using Inno.VisualNovel.Authoring.Diagnostics;

namespace Inno.VisualNovel.Authoring.LocalizationEditing;

/// <summary>Exchanges CSV/TSV translations while keeping engine-native assets as the storage authority.</summary>
public static class LocalizationDelimitedText
{
    private static readonly string[] C_COLUMNS = ["id", "source", "language", "translation", "status", "sourceFingerprint", "voice"];
    /// <summary>Gets the semantic columns accepted by explicit import mappings, in export order.</summary>
    public static IReadOnlyList<string> columns { get; } = Array.AsReadOnly(C_COLUMNS);
    /// <summary>Exports all declared target languages with quoted multiline Unicode text.</summary>
    /// <param name="table">The native source table.</param>
    /// <param name="separator">Comma or tab.</param>
    /// <returns>Exchange text, never a replacement native asset format.</returns>
    public static string Export(
        LocalizationTableAsset table,
        char separator = ','
    ) {
        CheckSeparator(separator); var lines = new List<string> { Row(C_COLUMNS) };
        foreach (LocalizedText entry in table.entries)
            foreach (LanguageDefinition language in table.languages.Where(language => language.id != table.sourceLanguage))
            {
                var translation = entry.translations.SingleOrDefault(item => item.language == language.id);
                lines.Add(Row([entry.id, entry.source, language.id, translation?.text ?? "", (translation?.status ?? TranslationStatus.Draft).ToString(),
                    translation?.sourceFingerprint ?? "", translation?.voiceCueId ?? ""]));
            }
        return string.Join("\r\n", lines) + "\r\n";
        string Row(IEnumerable<string> cells) => string.Join(separator, cells.Select(static cell => "\"" + cell.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""));
    }
    /// <summary>Parses, maps and validates exchange rows without mutating source or translations.</summary>
    /// <param name="table">The current destination.</param>
    /// <param name="text">CSV or TSV including a header.</param>
    /// <param name="separator">Comma or tab.</param>
    /// <param name="columnMapping">Optional explicit header-name to zero-based column mapping.</param>
    /// <returns>A conflict preview bound to this exact table content.</returns>
    public static LocalizationImportPreview Preview(
        LocalizationTableAsset table,
        string text,
        char separator = ',',
        IReadOnlyDictionary<string, int>? columnMapping = null
    ) {
        CheckSeparator(separator);
        var parsed = DelimitedTextTable.Parse(text, separator);
        if (parsed.Count == 0) throw new AuthoringValidationException($"翻译交换需要一行表头。");
        var columns = C_COLUMNS.ToDictionary(name => name, name => columnMapping is not null && columnMapping.TryGetValue(name, out int mapped) ? mapped
            : parsed[0].Count(header => header.Equals(name, StringComparison.OrdinalIgnoreCase)) == 1
                ? Array.FindIndex(parsed[0], header => header.Equals(name, StringComparison.OrdinalIgnoreCase)) : -1, StringComparer.Ordinal);
        if (columnMapping is not null && columnMapping.Keys.Any(name => !C_COLUMNS.Contains(name, StringComparer.Ordinal))
            || columns.Any(item => item.Value < 0 || item.Value >= parsed[0].Length) || columns.Values.Distinct().Count() != columns.Count)
            throw new AuthoringValidationException($"请将全部七个翻译字段映射到不同的有效表头列；重复列名需要明确选择位置。");
        var existing = table.entries.ToDictionary(static row => row.id, StringComparer.Ordinal);
        var result = new List<LocalizationImportRow>();
        foreach (string[] cells in parsed.Skip(1).Where(static cells => cells.Length != 1 || cells[0].Length != 0))
        {
            string Cell(string name) => columns[name] < cells.Length ? cells[columns[name]] : string.Empty;
            string id = Cell("id"), source = Cell("source"), language = Cell("language"), translated = Cell("translation"), hash = Cell("sourceFingerprint"), voice = Cell("voice");
            if (result.Count >= 100000) throw new AuthoringValidationException($"每批翻译导入最多十万条记录，请拆分文件。");
            TranslationStatus status = Enum.TryParse(Cell("status"), true, out TranslationStatus parsedStatus) ? parsedStatus : (TranslationStatus)(-1);
            result.Add(Review(table, existing, new(id, source, language, translated, status, hash, voice, "", true)));
        }
        var duplicates = result.GroupBy(static row => (row.id, row.language)).Where(static group => group.Count() > 1).Select(static group => group.Key).ToHashSet();
        var conflicts = result.GroupBy(static row => row.id, StringComparer.Ordinal).Where(static group => group.Select(static row => row.source).Distinct(StringComparer.Ordinal).Skip(1).Any())
            .Select(static group => group.Key).ToHashSet(StringComparer.Ordinal);
        return new(Digest(table), Array.AsReadOnly(result.Select(row => conflicts.Contains(row.id) ? row with { applicable = false, message = "同一文本 ID 的不同语言提供了不同原文。" }
            : duplicates.Contains((row.id, row.language)) ? row with { applicable = false, message = "同一文本及语言在导入中重复。" } : row).ToArray()));
    }
    /// <summary>Applies reviewed valid rows only when the native table has not changed since preview.</summary>
    /// <param name="table">The detached transaction candidate.</param>
    /// <param name="preview">An explicitly reviewed preview.</param>
    public static void Apply(
        LocalizationTableAsset table,
        LocalizationImportPreview preview
    ) {
        if (Digest(table) != preview.baseline) throw new AuthoringValidationException($"翻译表在导入预览后已改变，请重新预览再应用。");
        var reviewed = preview.rows.ToArray();
        if (reviewed.Length is < 1 or > 100000) throw new AuthoringValidationException($"每批翻译导入需要 1～100000 条记录。");
        if (reviewed.Sum(static row => (long)row.id.Length + row.source.Length + row.language.Length + row.text.Length + row.fingerprint.Length + row.voice.Length) > 16 * 1024 * 1024)
            throw new AuthoringValidationException($"翻译导入超过 16 MiB 预览预算。");
        var existing = table.entries.ToDictionary(static row => row.id, StringComparer.Ordinal);
        var duplicated = reviewed.GroupBy(static row => (row.id, row.language)).Where(static group => group.Count() > 1).Select(static group => group.Key).ToHashSet();
        var conflicts = reviewed.GroupBy(static row => row.id, StringComparer.Ordinal).Where(static group => group.Select(static row => row.source).Distinct(StringComparer.Ordinal).Skip(1).Any())
            .Select(static group => group.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var input in reviewed.Where(static row => row.applicable))
        {
            var checkedRow = Review(table, existing, input);
            if (!checkedRow.applicable || checkedRow.fingerprint != input.fingerprint || duplicated.Contains((input.id, input.language)) || conflicts.Contains(input.id))
                throw new AuthoringValidationException($"翻译导入记录复核失败：{input.id} / {input.language}。请重新读取并预览。");
        }
        if (!reviewed.Any(static row => row.applicable)) return;
        var rows = table.entries.Select(row => new LocalizedText { id = row.id, source = row.source, translations = row.translations.ToArray() }).ToDictionary(static row => row.id, StringComparer.Ordinal);
        foreach (var input in reviewed.Where(static row => row.applicable))
        {
            if (!rows.TryGetValue(input.id, out var row)) rows.Add(input.id, row = new() { id = input.id, source = input.source });
            row.translations = [.. row.translations.Where(item => item.language != input.language), new() { language = input.language, text = input.text,
                status = input.status, sourceFingerprint = input.fingerprint, voiceCueId = input.voice }];
        }
        var candidate = new LocalizationTableAsset { sourceLanguage = table.sourceLanguage, languages = table.languages,
            entries = rows.Values.OrderBy(static row => row.id, StringComparer.Ordinal).ToArray() };
        _ = new NovelLocalizationService(candidate, candidate.sourceLanguage);
        table.entries = candidate.entries;
    }
    private static LocalizationImportRow Review(
        LocalizationTableAsset table,
        IReadOnlyDictionary<string, LocalizedText> existing,
        LocalizationImportRow input
    ) {
        string message = ""; bool apply = true;
        if (string.IsNullOrWhiteSpace(input.id) || input.id.Length > 4096 || !table.languages.Any(item => item.id == input.language) || input.language == table.sourceLanguage)
        { message = "文本 ID 或目标语言无效。"; apply = false; }
        if (!Enum.IsDefined(input.status)) { message = "翻译状态无效。"; apply = false; }
        if (existing.TryGetValue(input.id, out var current) && current.source != input.source) { message = "原文与当前表冲突；不会覆盖原文。"; apply = false; }
        string expected = NovelLocalizationService.Fingerprint(input.source), hash = input.fingerprint.Length == 0 ? expected : input.fingerprint;
        if (apply && hash != expected) message = "原文版本需复核；运行时保留原文。";
        if (apply && input.status != TranslationStatus.Draft && hash == expected)
            try { NovelLocalizationService.RequireMatchingPlaceholders(input.source, input.text); }
            catch (InvalidOperationException) { message = "翻译中的变量占位符必须与原文一致且完整闭合。"; apply = false; }
        return input with { fingerprint = hash, message = message, applicable = apply };
    }
    private static string Digest(LocalizationTableAsset table)
    {
        var text = new StringBuilder(); Add(table.sourceLanguage);
        foreach (var language in table.languages) { Add(language.id); Add(language.name); }
        foreach (var row in table.entries)
        {
            Add(row.id); Add(row.source);
            foreach (var item in row.translations) { Add(item.language); Add(item.text); Add(item.status.ToString()); Add(item.sourceFingerprint); Add(item.voiceCueId); }
        }
        return NovelLocalizationService.Fingerprint(text.ToString());
        void Add(string value) => text.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
    }
    private static void CheckSeparator(char separator)
    { if (separator is not (',' or '\t')) throw new ArgumentException("Use comma-separated CSV or tab-separated TSV.", nameof(separator)); }
}

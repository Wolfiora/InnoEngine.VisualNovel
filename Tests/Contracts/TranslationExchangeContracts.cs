using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Authoring.LocalizationEditing;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Player.Localization;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class TranslationExchangeContracts
{
    internal static void MappingsAndCompleteReview()
    {
        var table = Table();
        string header = "Notes,Translated,Source,Language,Identity,Review,Revision,Recording";
        string exchange = header + "\n" + string.Join('\n', Enumerable.Range(0, 81).Select(index => $"ignored,Hello {{name}} {index},原文 {{name}},en,line-{index},Reviewed,,voice-{index}"));
        var mapping = new Dictionary<string, int>(StringComparer.Ordinal) { ["id"] = 4, ["source"] = 2, ["language"] = 3, ["translation"] = 1, ["status"] = 5, ["sourceFingerprint"] = 6, ["voice"] = 7 };
        var preview = LocalizationDelimitedText.Preview(table, exchange, ',', mapping);
        Check(preview.rows.Count == 81 && preview.rows.All(static row => row.applicable) && preview.rows[80].id == "line-80" && preview.rows[80].voice == "voice-80",
            "Explicit nonstandard header positions omitted later review records or altered recording identities.");
        LocalizationDelimitedText.Apply(table, preview);
        Check(table.entries.Length == 81 && new NovelLocalizationService(table, "en").Resolve("line-80", "原文 {name}") == "Hello {name} 80", "Mapped translations did not reach the actual runtime localization service.");
        mapping["voice"] = 99;
        Reject(() => LocalizationDelimitedText.Preview(Table(), exchange, ',', mapping), "An out-of-header position silently became an empty recording column.");
        mapping["voice"] = 1;
        Reject(() => LocalizationDelimitedText.Preview(Table(), exchange, ',', mapping), "Two semantic fields shared one mapped column.");
        var automatic = LocalizationDelimitedText.Preview(Table(), "ID,SOURCE,LANGUAGE,TRANSLATION,STATUS,SOURCEFINGERPRINT,VOICE\nx,原文,en,Hello,reviewed,,");
        Check(automatic.rows.Single().applicable, "Unambiguous header names and review states did not accept case-insensitive matching.");
        Reject(() => LocalizationDelimitedText.Preview(Table(), "id,source,language,translation,status,sourceFingerprint,voice,id\nx,原文,en,Hello,Reviewed,,,x"), "Ambiguous duplicate header names were guessed.");
        Check(LocalizationDelimitedText.columns.SequenceEqual(new[] { "id", "source", "language", "translation", "status", "sourceFingerprint", "voice" }), "Editor column metadata diverged from the actual exchange contract.");
    }
    internal static void AtomicApplicationAndCrossLanguageConflicts()
    {
        var table = Table();
        string header = "id,source,language,translation,status,sourceFingerprint,voice\n";
        var preview = LocalizationDelimitedText.Preview(table, header + "first,Hello {name},en,Hi {name},Reviewed,,\nsecond,Second,en,Two,Reviewed,,");
        string before = LocalizationDelimitedText.Export(table);
        var good = preview.rows[0]; var bad = preview.rows[1] with { language = "unknown", applicable = true };
        Reject(() => LocalizationDelimitedText.Apply(table, preview with { rows = new[] { good, bad } }), "A forged language bypassed review.");
        Check(LocalizationDelimitedText.Export(table) == before, "A later invalid row left an earlier translation applied.");
        NovelLocalizationService.RequireMatchingPlaceholders("Literal {{name}} and {name}", "{name} and literal {{name}}");
        Reject(() => NovelLocalizationService.RequireMatchingPlaceholders("Broken {outer{inner}", "Broken {outer{inner}"), "Localization used a weaker placeholder parser than authored dialogue.");
        Reject(() => LocalizationDelimitedText.Apply(table, preview with { rows = new[] { good with { text = "Missing variable" } } }), "A forged reviewed translation removed a source variable.");
        Reject(() => LocalizationDelimitedText.Apply(table, preview with { rows = new[] { good, good } }), "A forged duplicate silently replaced a translation.");
        var divergent = LocalizationDelimitedText.Preview(table, header + "new,First,en,One,Reviewed,,\nnew,Different,ja,一,Reviewed,,");
        Check(divergent.rows.Count == 2 && divergent.rows.All(static row => !row.applicable && row.message.Length > 0), "Different source text for one new identity was accepted across languages.");
        Reject(() => LocalizationDelimitedText.Apply(table, divergent with { rows = divergent.rows.Select(static row => row with { applicable = true }).ToArray() }), "Forged cross-language source conflicts bypassed review.");
        LocalizationDelimitedText.Apply(table, preview);
        var unchanged = table.entries;
        LocalizationDelimitedText.Apply(table, LocalizationDelimitedText.Preview(table, header + "bad,Source,xx,Translation,Reviewed,,"));
        Check(ReferenceEquals(unchanged, table.entries), "An entirely inapplicable review reordered or replaced existing source data.");
        var sourceConflict = LocalizationDelimitedText.Preview(table, header + "first,Changed,en,Changed,Reviewed,,");
        Check(!sourceConflict.rows.Single().applicable, "An existing source conflict became applicable.");
        Reject(() => LocalizationDelimitedText.Apply(table, sourceConflict with { rows = sourceConflict.rows.Select(static row => row with { applicable = true }).ToArray() }), "Explicit applicability overrode the current source authority.");
        var current = LocalizationDelimitedText.Preview(table, header + "first,Hello {name},ja,こんにちは {name},Reviewed,,");
        table.languages[2].name = "Changed language metadata";
        Reject(() => LocalizationDelimitedText.Apply(table, current), "A language metadata change reused a stale review baseline.");
    }
    private static LocalizationTableAsset Table() => new() { languages = [new(), new() { id = "en", name = "English" }, new() { id = "ja", name = "日本語" }] };
    private static void Reject(
        Action action,
        string message
    ) {
        bool rejected = false;
        try { action(); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, message);
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}

using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.LocalizationEditing;
using Inno.VisualNovel.Editor.Localization;
using Inno.VisualNovel.Model.Localization;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class EditorLanguageContracts
{
    internal static void NativeCatalogIdentityAndIsolation(SerializationRegistry native)
    {
        var table = Create(); string literal = "显示 {player} 和 {{brace}} }", format = "章节 {0} · {1:0.00}##node/{2}";
        Add("标题", "Title"); Add("上移##", "Move up##"); Add(literal, "Show {player} and {{brace}} }"); Add(format, "Chapter {0} · {1:0.00}##node/{2}", true);
        Add("变量 · ", "Variable · "); Add("页面 · ", "Page · "); Add(" · 随游戏保存", " · Saved with game"); Add(" · 本次页面", " · Current screen only");
        var row = native.Deserialize<LocalizedText>(native.Serialize(table.entries[2]));
        Check(row.source == EditorTextCatalog.EncodeLiteral(literal), "Native editor resources lost escaped literal braces.");
        var first = new NovelEditorLanguageModule(); var second = new NovelEditorLanguageModule(); first.Use(table, "en");
        Check(first.catalog.Text("标题") == "Title" && second.catalog.Text("标题") == "标题" && first.catalog.language == "en" && second.catalog.language == "zh-CN",
            "Editor locale state escaped its native project owner or changed another editor.");
        Check(first.catalog.Text("上移##owner-17") == "Move up##owner-17" && first.catalog.Text(literal) == "Show {player} and {{brace}} }",
            "An appended native UI identity or a literal game-variable example was translated as mutable story data.");
        string englishLabel = first.catalog.Label("上移##owner-17"), sourceLabel = second.catalog.Label("上移##owner-17");
        Check(englishLabel == "Move up###inno.vn.editor/上移##owner-17" && sourceLabel == "上移###inno.vn.editor/上移##owner-17"
            && first.catalog.Label("标题###fixed-owner") == "Title###fixed-owner", "Localized controls confused hidden double hashes with the native stable triple-hash identity protocol.");
        Check(first.catalog.Format(format, "澄{custom}", 1.25, "stable-id") == "Chapter 澄{custom} · 1.25##node/stable-id",
            "Editor formatting translated supplied user values or changed numeric formatting and native identity.");
        string formattedLabel = first.catalog.FormatLabel(format, "澄{custom}", 1.25, "stable-id");
        string sourceFormattedLabel = second.catalog.FormatLabel(format, "澄{custom}", 1.25, "stable-id");
        Check(formattedLabel.StartsWith("Chapter 澄{custom} · 1.25###inno.vn.editor/", StringComparison.Ordinal)
            && formattedLabel[formattedLabel.IndexOf("###", StringComparison.Ordinal)..] == sourceFormattedLabel[sourceFormattedLabel.IndexOf("###", StringComparison.Ordinal)..],
            "Composite labels derived native identities from their translated captions.");
        string composed = first.catalog.Label("标题 · 澄##owner", first.catalog.Text("标题") + " · 澄##owner");
        Check(composed == "Title · 澄###inno.vn.editor/标题 · 澄##owner", "Composed captions translated user names or derived identity from translated parts.");
        Reject(() => first.catalog.Label("标题##owner", "Title##other-owner"));
        var choices = first.catalog.BuiltInChoices(new[] { ("title", "标题"), ("variables.title", "变量 · 标题{raw} · title"),
            ("page.saved", "页面 · 标题 · 随游戏保存 {raw} · 随游戏保存 · saved"), ("page.local", "页面 · 标题 · 本次页面 · local"), ("variables.unformatted", "标题") }).ToArray();
        Check(choices[0].name == "Title" && choices[1].name == "Variable · 标题{raw} · title"
            && choices[2].name == "Page · 标题 · 随游戏保存 {raw} · Saved with game · saved" && choices[3].name == "Page · 标题 · Current screen only · local"
            && choices[4].name == "标题", "Built-in binding translations changed authored variable names, literal braces, IDs or lifetime ownership.");
        table.entries[0].translations[0].text = "Changed later";
        Check(first.catalog.Text("标题") == "Title", "An installed editor catalog retained a mutable native source row.");
        var invalid = Create(); invalid.entries = [new() { id = EditorTextCatalog.Key("标题##original"), source = "标题##original" }];
        LocalizationEditService.SetTranslation(invalid, invalid.entries[0].id, "en", "Title##different", TranslationStatus.Reviewed);
        Reject(() => first.Use(invalid, "en")); Check(first.catalog.Text("标题") == "Title", "An invalid language candidate replaced the last valid view.");
        invalid = Create(); invalid.entries = [new() { id = "editor.literal.wrong-id", source = "标题" }]; Reject(() => first.Use(invalid, "en"));
        first.Reset(); Check(first.catalog.language == "zh-CN" && first.catalog.Text(literal) == literal && second.catalog.language == "zh-CN", "Resetting language retained resources or evaluated literal braces.");
        void Add(
            string source,
            string translation,
            bool composite = false
        ) {
            string key = EditorTextCatalog.Key(source, composite); table.entries = [.. table.entries, new() { id = key, source = composite ? source : EditorTextCatalog.EncodeLiteral(source) }];
            LocalizationEditService.SetTranslation(table, key, "en", composite ? translation : EditorTextCatalog.EncodeLiteral(translation), TranslationStatus.Reviewed);
        }
    }
    private static LocalizationTableAsset Create() => new() { sourceLanguage = "zh-CN", languages = [new(), new() { id = "en", name = "English" }] };
    private static void Reject(Action operation)
    { try { operation(); } catch (InvalidOperationException) { return; } throw new InvalidOperationException("An invalid native editor language resource was accepted."); }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}

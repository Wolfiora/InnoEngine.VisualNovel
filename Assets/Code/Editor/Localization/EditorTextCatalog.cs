using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Model.Text;
using Inno.VisualNovel.Player.Localization;
using Inno.VisualNovel.Authoring.Diagnostics;

namespace Inno.VisualNovel.Editor.Localization;

/// <summary>Freezes editor translations through the native VN localization contract without changing game language or global culture.</summary>
public sealed class EditorTextCatalog
{
    private readonly NovelLocalizationService m_text;
    /// <summary>Creates an isolated view of a native editor translation table.</summary>
    /// <param name="table">The native table, or null for the built-in Simplified Chinese source view.</param>
    /// <param name="language">The explicitly selected editor language; non-source views require a declared native table.</param>
    public EditorTextCatalog(
        LocalizationTableAsset? table,
        string language = "zh-CN"
    ) {
        if (table is null && language != "zh-CN") throw new ArgumentException("An editor translation table is required for this language.", nameof(language));
        if (table is not null && table.sourceLanguage != "zh-CN") throw new ArgumentException("Editor source resources use zh-CN independently of the game's source language.", nameof(table));
        m_text = new(table, language);
        foreach (var row in table?.entries ?? [])
        {
            bool format = row.id.StartsWith("editor.format.", StringComparison.Ordinal);
            string source = format ? row.source : DecodeLiteral(row.source);
            if (row.id != Key(source, format)) throw new InvalidOperationException("Editor translation identity does not match its exact source: " + row.id);
            string translated = m_text.Resolve(row.id, row.source);
            PreserveIdentity(source, format ? translated : DecodeLiteral(translated));
            if (format) { _ = CompositeFormat.Parse(source); _ = CompositeFormat.Parse(translated); }
        }
    }
    /// <summary>Gets the selected editor language independently of all game sessions.</summary>
    public string language => m_text.language;
    /// <summary>Translates an authoring failure's declared source while preserving user names, identities and ordinary exception semantics.</summary>
    /// <param name="error">A caught authoring or host failure, never arbitrary authored story text.</param>
    /// <returns>The current owner's message; unknown host messages retain their original text.</returns>
    public string Error(Exception error) => error switch
    {
        AuthoringValidationException validation => Format(validation.source, validation.arguments.ToArray()),
        AuthoringArgumentException argument => Format(argument.source, argument.arguments.ToArray()),
        _ => error.Message
    };
    /// <summary>Computes the stable identity shared by native editor resources and the source text inventory.</summary>
    /// <param name="source">The exact unescaped source literal or composite format.</param>
    /// <param name="format">Whether the source is a composite format rather than a literal.</param>
    /// <returns>A source-addressable native localization identity.</returns>
    public static string Key(
        string source,
        bool format = false
    ) => "editor." + (format ? "format." : "literal.") + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)))[..24].ToLowerInvariant();
    /// <summary>Escapes literal braces before storing an editor label in a native translation row.</summary>
    /// <param name="source">Literal UI text, not a story variable expression.</param>
    /// <returns>A native localization source containing no variable placeholders.</returns>
    public static string EncodeLiteral(string source) => source.Replace("{", "{{", StringComparison.Ordinal).Replace("}", "}}", StringComparison.Ordinal);
    /// <summary>Translates an explicit UI label while preserving its native ImGui identity suffix, including appended owner IDs.</summary>
    /// <param name="source">A developer-authored label; user data and asset names must not be passed automatically.</param>
    /// <returns>The current translation or the unchanged authored fallback.</returns>
    public string Text(string source)
    {
        string translated = Literal(source);
        if (translated != source) return PreserveIdentity(source, translated);
        int separator = source.IndexOf("##", StringComparison.Ordinal);
        if (separator < 0) return source;
        string prefix = source[..(separator + 2)], head = Literal(prefix);
        if (head == prefix) head = Literal(source[..separator]) + "##";
        return PreserveIdentity(prefix, head) + source[(separator + 2)..];
    }
    /// <summary>Builds an interactive ImGui label whose identity remains the same in every editor language.</summary>
    /// <param name="source">The complete source label, including any dynamic owner suffix.</param>
    /// <returns>Translated visible text and a language-independent triple-hash identity; explicit triple-hash identities are retained.</returns>
    public string Label(string source) => InteractiveLabel(source, Text(source));
    /// <summary>Combines an unchanged source identity with a separately translated composite caption.</summary>
    /// <param name="source">The complete untranslated label, including its owner suffix.</param>
    /// <param name="caption">The caption assembled from translated developer literals and unchanged user values.</param>
    /// <returns>A label whose native identity is independent of translated caption parts.</returns>
    public string Label(
        string source,
        string caption
    ) => InteractiveLabel(source, PreserveIdentity(source, caption));
    /// <summary>Translates declared built-in selector metadata while preserving user-authored variable names, field names and IDs.</summary>
    /// <param name="choices">Only the output of Authoring BuiltInUiChoices; arbitrary user option lists must remain unchanged.</param>
    /// <returns>The same ordered identities with translated fixed labels and lifetime annotations.</returns>
    public IEnumerable<(string id, string name)> BuiltInChoices(IEnumerable<(string id, string name)> choices)
    {
        foreach (var item in choices)
        {
            string caption;
            if (item.id.StartsWith("variables.", StringComparison.Ordinal))
                caption = item.name.StartsWith("变量 · ", StringComparison.Ordinal) ? Text("变量 · ") + item.name["变量 · ".Length..] : item.name;
            else if (item.id.StartsWith("page.", StringComparison.Ordinal)) caption = PageField(item.id["page.".Length..], item.name);
            else caption = Text(item.name);
            yield return (item.id, caption);
        }
    }
    private string PageField(
        string id,
        string caption
    ) {
        const string prefix = "页面 · ";
        if (!caption.StartsWith(prefix, StringComparison.Ordinal)) return caption;
        foreach (string lifetime in new[] { " · 随游戏保存", " · 本次页面" })
        {
            string suffix = lifetime + " · " + id;
            if (caption.EndsWith(suffix, StringComparison.Ordinal)) return Text(prefix) + caption[prefix.Length..^suffix.Length] + Text(lifetime) + " · " + id;
        }
        return caption;
    }
    /// <summary>Translates a composite UI format before inserting explicit values; values are never themselves translated.</summary>
    /// <param name="source">A developer-authored composite format matching the resource inventory.</param>
    /// <param name="arguments">Values inserted after template resolution.</param>
    /// <returns>Formatted text with stable native identity suffixes and invariant numeric formatting.</returns>
    public string Format(
        string source,
        params object?[] arguments
    ) => string.Format(CultureInfo.InvariantCulture, PreserveIdentity(source, m_text.Resolve(Key(source, true), source)), arguments);
    /// <summary>Formats a translated interactive label while deriving its ImGui identity from the source values.</summary>
    /// <param name="source">The source composite format, including stable author-supplied owner fields.</param>
    /// <param name="arguments">Values inserted into the source identity and translated display independently.</param>
    /// <returns>A formatted label with the same identity across language changes.</returns>
    public string FormatLabel(
        string source,
        params object?[] arguments
    ) => InteractiveLabel(string.Format(CultureInfo.InvariantCulture, source, arguments), Format(source, arguments));
    private static string InteractiveLabel(
        string source,
        string translated
    ) {
        int visible = translated.IndexOf("##", StringComparison.Ordinal), stable = source.LastIndexOf("###", StringComparison.Ordinal);
        string caption = visible < 0 ? translated : translated[..visible];
        return caption + (stable < 0 ? "###inno.vn.editor/" + source : source[stable..]);
    }
    private string Literal(string source)
    {
        string encoded = EncodeLiteral(source);
        string value = m_text.Resolve(Key(source), encoded);
        return DecodeLiteral(value);
    }
    private static string DecodeLiteral(string source)
    {
        var tokens = NovelTextTemplate.Parse(source);
        if (tokens.Any(static token => token.variable)) throw new InvalidOperationException("Editor literal translations cannot evaluate game variables.");
        return string.Concat(tokens.Select(static token => token.value));
    }
    private static string PreserveIdentity(
        string source,
        string translated
    ) {
        int original = source.IndexOf("##", StringComparison.Ordinal), candidate = translated.IndexOf("##", StringComparison.Ordinal);
        if ((original < 0) != (candidate < 0) || original >= 0 && source[original..] != translated[candidate..])
            throw new InvalidOperationException("An editor translation changed its native control identity: " + source);
        return translated;
    }
}

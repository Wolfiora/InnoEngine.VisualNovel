using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace Inno.VisualNovel.Model.Text;

/// <summary>Contains normalized immutable text, styling and authored pacing without engine resources.</summary>
public sealed class NovelTextDocument
{
    internal NovelTextDocument(
        string markup,
        string text,
        NovelTextRun[] runs,
        NovelTextBeat[] beats
    ) {
        this.markup = markup; this.text = text; this.runs = Array.AsReadOnly(runs); this.beats = Array.AsReadOnly(beats);
        elements = Array.AsReadOnly(StringInfo.ParseCombiningCharacters(text));
    }
    /// <summary>Gets resolved markup, with interpolated data escaped as literal text.</summary>
    public string markup { get; }
    /// <summary>Gets plain text including object replacement characters for inline images.</summary>
    public string text { get; }
    /// <summary>Gets immutable paint ranges.</summary>
    public IReadOnlyList<NovelTextRun> runs { get; }
    /// <summary>Gets authored pacing events in source order.</summary>
    public IReadOnlyList<NovelTextBeat> beats { get; }
    /// <summary>Gets grapheme boundaries shared by layout and reading.</summary>
    public ReadOnlyCollection<int> elements { get; }
    /// <summary>Gets a plain prefix without splitting a Unicode text element.</summary>
    /// <param name="count">The requested visible grapheme count.</param>
    /// <returns>The bounded visible prefix.</returns>
    public string Prefix(int count) => count <= 0 ? string.Empty : count >= elements.Count ? text : text[..elements[count]];
    /// <summary>Creates a readable paint variant while retaining text, emphasis, ruby, icons, pacing and reveal positions.</summary>
    /// <param name="foreground">The accessibility foreground applied to all text runs.</param>
    /// <returns>An independent immutable document; authored markup remains unchanged.</returns>
    public NovelTextDocument WithForeground(uint foreground) => new(markup, text,
        runs.Select(run => run with { style = run.style with { color = foreground, outlineColor = 0x000000FF, shadowColor = 0x000000FF } }).ToArray(), beats.ToArray());
    /// <summary>Creates literal content without interpreting authoring tags.</summary>
    /// <param name="text">Untrusted or ordinary display text.</param>
    /// <returns>An immutable unstyled document.</returns>
    public static NovelTextDocument Literal(string text) => new(NovelTextMarkup.Escape(text), text, text.Length == 0 ? [] : [new(0, text.Length, new())], []);
}

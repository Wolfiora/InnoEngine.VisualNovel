using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using InnoEngine.Text;

namespace Inno.VisualNovel.UI.Text;

/// <summary>Selects whole graphemes from an ordered font chain while delegating shaping to the native engine contract.</summary>
public sealed class FontFallbackResolver
{
    private readonly Func<FontAsset, string, TextStyle, TextLayout> m_shape;
    /// <summary>Uses the engine shaper or a host adapter with the same UTF-8 cluster contract.</summary>
    /// <param name="shape">An optional headless/native shaping adapter.</param>
    public FontFallbackResolver(Func<FontAsset, string, TextStyle, TextLayout>? shape = null) => m_shape = shape ?? ((font, text, style) => InnoEngine.Text.Text.Shape(font, text, style));
    /// <summary>Shapes contiguous runs of the selected font, preserving ligatures within each run and complete grapheme ownership.</summary>
    /// <param name="primary">The authored primary face.</param>
    /// <param name="text">The complete styled segment.</param>
    /// <param name="style">The native engine style.</param>
    /// <param name="fallbacks">The ordered borrowed fallback chain.</param>
    /// <returns>Native glyphs, matching font identities and unresolved whole graphemes.</returns>
    public FontFallbackShape Shape(
        FontAsset primary,
        string text,
        TextStyle style,
        IReadOnlyList<FontAsset> fallbacks
    ) {
        var original = m_shape(primary, text, style);
        if (!original.glyphs.Any(static glyph => glyph.glyphId == 0))
            return new(original, Array.AsReadOnly(Enumerable.Repeat(primary, original.glyphs.Count).ToArray()), []);
        var chain = new[] { primary }.Concat(fallbacks).Distinct().ToArray();
        int[] starts = StringInfo.ParseCombiningCharacters(text);
        var selected = new FontAsset[starts.Length]; var missing = new List<FontMissingCluster>();
        var choices = new Dictionary<string, (FontAsset font, bool complete)>(StringComparer.Ordinal);
        for (int index = 0; index < starts.Length; index++)
        {
            string element = text[starts[index]..(index + 1 == starts.Length ? text.Length : starts[index + 1])];
            if (!choices.TryGetValue(element, out var choice))
            {
                choice = (primary, false);
                foreach (var font in chain)
                    if (!m_shape(font, element, style).glyphs.Any(static glyph => glyph.glyphId == 0)) { choice = (font, true); break; }
                choices.Add(element, choice);
            }
            selected[index] = choice.font;
            if (!choice.complete) missing.Add(new(starts[index], element));
        }
        var glyphs = new List<TextGlyph>(); var fonts = new List<FontAsset>();
        float width = 0, height = 0, ascender = original.metrics.ascender, descender = original.metrics.descender, lineHeight = original.metrics.lineHeight;
        int byteOffset = 0;
        for (int first = 0; first < starts.Length;)
        {
            int next = first + 1;
            while (next < starts.Length && ReferenceEquals(selected[next], selected[first])) next++;
            string segment = text[starts[first]..(next == starts.Length ? text.Length : starts[next])];
            var shaped = m_shape(selected[first], segment, style);
            foreach (var glyph in shaped.glyphs)
            {
                glyphs.Add(new(glyph.glyphId, checked(glyph.cluster + (uint)byteOffset), glyph.advanceX, glyph.advanceY, glyph.offsetX, glyph.offsetY)); fonts.Add(selected[first]);
            }
            byteOffset += Encoding.UTF8.GetByteCount(segment); width += shaped.width; height = Math.Max(height, shaped.height);
            ascender = Math.Max(ascender, shaped.metrics.ascender); descender = Math.Min(descender, shaped.metrics.descender); lineHeight = Math.Max(lineHeight, shaped.metrics.lineHeight);
            first = next;
        }
        var metrics = new TextMetrics(ascender, descender, lineHeight, original.metrics.underlinePosition, original.metrics.underlineThickness);
        return new(new TextLayout(glyphs, metrics, width, height), fonts.AsReadOnly(), missing.AsReadOnly());
    }
}

/// <summary>Contains native shaped glyphs and the matching borrowed font for each glyph.</summary>
/// <param name="layout">The combined native layout with original segment-relative UTF-8 clusters.</param>
/// <param name="fonts">One exact font identity per glyph.</param>
/// <param name="missing">Unresolved whole graphemes relative to the segment's UTF-16 text.</param>
public sealed record FontFallbackShape(TextLayout layout, IReadOnlyList<FontAsset> fonts, IReadOnlyList<FontMissingCluster> missing);

/// <summary>Identifies one grapheme missing from every declared face.</summary>
/// <param name="start">Its UTF-16 position within the styled segment.</param>
/// <param name="text">The complete unresolved grapheme.</param>
public sealed record FontMissingCluster(int start, string text);

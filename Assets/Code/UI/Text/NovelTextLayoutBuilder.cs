using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InnoEngine.Text;
using InnoEngine.Rendering;
using Inno.VisualNovel.Model.Text;
using Inno.VisualNovel.Model.Interface;

namespace Inno.VisualNovel.UI.Text;

/// <summary>Shapes full styled ranges, wraps complete clusters and lays out ruby without reveal-driven reflow.</summary>
public sealed class NovelTextLayoutBuilder
{
    private readonly FontFallbackResolver m_fonts;
    /// <summary>Uses the engine text shaper or a host-supplied equivalent with UTF-8 cluster offsets.</summary>
    /// <param name="shape">An optional host adapter sharing the engine cluster contract.</param>
    public NovelTextLayoutBuilder(Func<FontAsset, string, TextStyle, TextLayout>? shape = null) => m_fonts = new(shape);

    /// <summary>Builds text-local layout using the full text before filtering the reveal boundary.</summary>
    /// <param name="content">Full content and the independent reveal count.</param>
    /// <param name="font">Inherited imported font.</param>
    /// <param name="size">Inherited logical font size.</param>
    /// <param name="width">Available width after text inset.</param>
    /// <param name="lineSpacing">Positive authored line-height multiplier.</param>
    /// <param name="alignment">Per-line alignment.</param>
    /// <param name="resources">Alternate fonts and inline images.</param>
    /// <param name="scale">Accessibility scale for absolute sizes.</param>
    /// <returns>Full bounds and currently visible geometry.</returns>
    public NovelTextLayout Build(
        UiRichText content,
        FontAsset font,
        float size,
        float width,
        float lineSpacing = 1.35f,
        UiTextAlignment alignment = UiTextAlignment.Left,
        UiRichTextResources? resources = null,
        float scale = 1
    ) {
        if (!float.IsFinite(size) || size <= 0 || !float.IsFinite(width) || width < 0 || !float.IsFinite(lineSpacing) || lineSpacing <= 0 || !float.IsFinite(scale) || scale <= 0 || !Enum.IsDefined(alignment))
            throw new ArgumentOutOfRangeException(nameof(size));
        resources ??= UiRichTextResources.empty;
        NovelTextDocument document = content.document; int visibleEnd = content.visibleGraphemes >= document.elements.Count ? document.text.Length : content.visibleGraphemes <= 0 ? 0 : document.elements[content.visibleGraphemes];
        var glyphs = new List<NovelTextGlyph>(); var images = new List<NovelTextImage>(); var line = new List<Unit>();
        var missing = new List<NovelMissingGlyph>();
        float y = 0, lineWidth = 0, usedWidth = 0; int runIndex = 0, elementIndex = 0;
        while (elementIndex < document.elements.Count)
        {
            int start = document.elements[elementIndex]; int end = End(elementIndex);
            while (runIndex + 1 < document.runs.Count && document.runs[runIndex].start + document.runs[runIndex].length <= start) runIndex++;
            NovelTextRun run = document.runs[runIndex]; NovelTextStyle style = run.style;
            float actualSize = style.size == 0 ? size : style.size * scale;
            FontAsset actualFont = style.font.Length == 0 ? font : resources.Font(style.font);
            if (document.text[start..end] is "\n" or "\r\n" or "\r") { FlushLine(true); elementIndex++; continue; }
            if (run.icon.Length > 0)
            {
                TextureAsset icon = resources.Icon(run.icon);
                float iconWidth = icon.height > 0 ? actualSize * icon.width / icon.height : actualSize;
                Add(new(iconWidth, actualSize * 0.8f, actualSize * 0.2f, actualSize * lineSpacing, [], icon, end)); elementIndex++; continue;
            }
            int first = elementIndex++;
            while (elementIndex < document.elements.Count && document.elements[elementIndex] < run.start + run.length
                && document.text[document.elements[elementIndex]] is not ('\r' or '\n')) elementIndex++;
            end = elementIndex == document.elements.Count ? document.text.Length : document.elements[elementIndex];
            string segment = document.text[start..end];
            TextStyle nativeStyle = new(actualSize, weight: style.bold ? 700 : 400, style: style.italic ? TextFontStyle.Italic : TextFontStyle.Normal);
            FontFallbackShape shapedFonts = m_fonts.Shape(actualFont, segment, nativeStyle, resources.fallbackFonts); TextLayout shaped = shapedFonts.layout;
            foreach (var uncovered in shapedFonts.missing) missing.Add(new(start + uncovered.start, uncovered.text, false));
            int[] byteOffsets = Utf8Offsets(segment); uint[] clusters = shaped.glyphs.Select(static glyph => glyph.cluster).Distinct().Order().ToArray();
            int ClusterEnd(uint cluster)
            {
                int next = Array.BinarySearch(clusters, cluster) + 1;
                int rawEnd = next < clusters.Length ? start + byteOffsets[Math.Min((int)clusters[next], byteOffsets.Length - 1)] : end;
                int low = first, high = document.elements.Count;
                while (low < high) { int middle = low + (high - low) / 2; if (document.elements[middle] < rawEnd) low = middle + 1; else high = middle; }
                return low < document.elements.Count ? document.elements[low] : document.text.Length;
            }
            if (run.ruby.Length > 0)
            {
                float rubySize = actualSize * 0.5f; var rubyFonts = m_fonts.Shape(actualFont, run.ruby, new(rubySize), resources.fallbackFonts); TextLayout ruby = rubyFonts.layout;
                foreach (var uncovered in rubyFonts.missing) missing.Add(new(start, uncovered.text, true));
                float unitWidth = Math.Max(shaped.width, ruby.width); var parts = new List<Part>();
                float pen = (unitWidth - shaped.width) * 0.5f;
                for (int index = 0; index < shaped.glyphs.Count; index++) { var glyph = shaped.glyphs[index]; parts.Add(new(shapedFonts.fonts[index], actualSize, glyph, pen, 0, style, ClusterEnd(glyph.cluster))); pen += glyph.advanceX; }
                pen = (unitWidth - ruby.width) * 0.5f;
                for (int index = 0; index < ruby.glyphs.Count; index++) { var glyph = ruby.glyphs[index]; parts.Add(new(rubyFonts.fonts[index], rubySize, glyph, pen, -shaped.metrics.ascender - rubySize * 0.15f, style with { bold = false, italic = false, outline = 0, shadow = 0 }, end)); pen += glyph.advanceX; }
                Add(new(unitWidth, Math.Max(actualSize * 0.8f, shaped.metrics.ascender) + ruby.height + rubySize * 0.15f,
                    Math.Max(actualSize * 0.2f, -shaped.metrics.descender), Math.Max(actualSize, shaped.height) * lineSpacing + ruby.height, parts, null, end));
                continue;
            }
            // Native clusters are UTF-8 byte offsets; convert them before comparing with UTF-16 grapheme boundaries.
            for (int glyphIndex = 0; glyphIndex < shaped.glyphs.Count;)
            {
                uint cluster = shaped.glyphs[glyphIndex].cluster; int revealEnd = ClusterEnd(cluster);
                var parts = new List<Part>(); float pen = 0;
                do { FontAsset glyphFont = shapedFonts.fonts[glyphIndex]; TextGlyph glyph = shaped.glyphs[glyphIndex++]; parts.Add(new(glyphFont, actualSize, glyph, pen, 0, style, revealEnd)); pen += glyph.advanceX; }
                while (glyphIndex < shaped.glyphs.Count && shaped.glyphs[glyphIndex].cluster == cluster);
                Add(new(Math.Max(0, pen), Math.Max(actualSize * 0.8f, shaped.metrics.ascender), Math.Max(actualSize * 0.2f, -shaped.metrics.descender),
                    Math.Max(actualSize, shaped.height) * lineSpacing, parts, null, revealEnd));
            }
        }
        FlushLine(line.Count > 0 || document.text.Length == 0 || document.text.EndsWith('\n'));
        float visibleHeight = Math.Max(glyphs.Count == 0 ? 0 : glyphs.Max(static glyph => glyph.baseline + glyph.size * 0.2f),
            images.Count == 0 ? 0 : images.Max(static image => image.bounds.y + image.bounds.height));
        return new(usedWidth, y, glyphs.AsReadOnly(), images.AsReadOnly(), content.visibleGraphemes >= document.elements.Count ? y : visibleHeight) { missingGlyphs = missing.AsReadOnly() };

        int End(int index) => index + 1 == document.elements.Count ? document.text.Length : document.elements[index + 1];
        void Add(Unit unit)
        {
            if (line.Count > 0 && lineWidth + unit.width > width) FlushLine(true);
            line.Add(unit); lineWidth += unit.width;
        }
        void FlushLine(bool includeEmpty)
        {
            if (!includeEmpty) return;
            float ascent = line.Count == 0 ? size * 0.8f : line.Max(static item => item.ascent);
            float descent = line.Count == 0 ? size * 0.2f : line.Max(static item => item.descent);
            float height = Math.Max(ascent + descent, line.Count == 0 ? size * lineSpacing : line.Max(static item => item.height));
            float pen = alignment == UiTextAlignment.Center ? Math.Max(0, (width - lineWidth) * 0.5f) : alignment == UiTextAlignment.Right ? Math.Max(0, width - lineWidth) : 0;
            foreach (Unit unit in line)
            {
                foreach (Part part in unit.parts)
                    if (part.end <= visibleEnd) glyphs.Add(new(part.font, part.size, part.glyph, pen + part.x, y + ascent + part.baseline, part.style));
                if (unit.icon is not null && unit.end <= visibleEnd) images.Add(new(unit.icon, new(pen, y + ascent - unit.ascent, unit.width, unit.ascent + unit.descent)));
                pen += unit.width;
            }
            usedWidth = Math.Max(usedWidth, lineWidth); y += height; line.Clear(); lineWidth = 0;
        }
    }

    private static int[] Utf8Offsets(string text)
    {
        var offsets = new int[Encoding.UTF8.GetByteCount(text) + 1]; int bytes = 0;
        for (int index = 0; index < text.Length;)
        {
            int length = char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]) ? 2 : 1;
            int count = Encoding.UTF8.GetByteCount(text.AsSpan(index, length));
            for (int offset = 0; offset < count; offset++) offsets[bytes++] = index;
            index += length; offsets[bytes] = index;
        }
        return offsets;
    }

    private sealed record Part(FontAsset font, float size, TextGlyph glyph, float x, float baseline, NovelTextStyle style, int end);
    private sealed record Unit(float width, float ascent, float descent, float height, IReadOnlyList<Part> parts, TextureAsset? icon, int end);
}

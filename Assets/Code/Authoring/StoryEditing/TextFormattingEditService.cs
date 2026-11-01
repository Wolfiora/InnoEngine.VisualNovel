using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Inno.VisualNovel.Model.Text;

namespace Inno.VisualNovel.Authoring.StoryEditing;

/// <summary>Applies visual text formatting to grapheme selections while preserving text, icons and pacing.</summary>
public static class TextFormattingEditService
{
    /// <summary>Replaces styling on a complete grapheme range without creating crossing tag scopes.</summary>
    /// <param name="source">Authored rich markup.</param>
    /// <param name="first">Zero-based first plain grapheme.</param>
    /// <param name="count">Number of complete selected graphemes.</param>
    /// <param name="style">The selected replacement style.</param>
    /// <returns>Equivalent markup with the selected style applied.</returns>
    public static string ApplyStyle(
        string source,
        int first,
        int count,
        NovelTextStyle style
    ) => Rewrite(source, first, count, style, null);

    /// <summary>Adds or removes ruby on one complete plain-text selection.</summary>
    /// <param name="source">Authored rich markup.</param>
    /// <param name="first">Zero-based first plain grapheme.</param>
    /// <param name="count">Number of selected graphemes.</param>
    /// <param name="annotation">The annotation; empty removes ruby.</param>
    /// <returns>Valid markup retaining the selected text and pacing outside the annotation.</returns>
    public static string ApplyRuby(
        string source,
        int first,
        int count,
        string annotation
    ) => Rewrite(source, first, count, null, annotation);

    /// <summary>Applies a local reveal-rate multiplier while retaining existing styles and pauses.</summary>
    /// <param name="source">Authored rich markup.</param>
    /// <param name="first">Zero-based first plain grapheme.</param>
    /// <param name="count">Number of selected graphemes.</param>
    /// <param name="speed">A finite multiplier from 0.01 through 100.</param>
    /// <returns>Valid markup with the selected rate.</returns>
    public static string ApplySpeed(
        string source,
        int first,
        int count,
        double speed
    ) {
        if (!double.IsFinite(speed) || speed is < 0.01 or > 100) throw new ArgumentOutOfRangeException(nameof(speed));
        return Rewrite(source, first, count, null, null, speed);
    }

    private static string Rewrite(
        string source,
        int first,
        int count,
        NovelTextStyle? replacement,
        string? annotation,
        double? speedOverride = null
    ) {
        NovelTextDocument document = NovelTextMarkup.Parse(source);
        if (first < 0 || count <= 0 || first > document.elements.Count - count) throw new ArgumentOutOfRangeException(nameof(first));
        int start = document.elements[first], end = first + count == document.elements.Count ? document.text.Length : document.elements[first + count];
        foreach (NovelTextRun run in document.runs.Where(static item => item.ruby.Length > 0))
            if (start > run.start && start < run.start + run.length || end > run.start && end < run.start + run.length)
                throw new InvalidOperationException("Select the entire existing ruby base before formatting it.");
        // An interpolation is one authoring token even though its eventual resolved length is unknown.
        var atomic = NovelTextTemplate.Parse(document.text).Where(static token => token.variable || token.length == 2 && token.value == "{").ToDictionary(static token => token.start);
        foreach (var token in atomic.Values)
        {
            if (start > token.start && start < token.start + token.length || end > token.start && end < token.start + token.length)
                throw new InvalidOperationException("Select the entire variable placeholder or escaped brace before formatting it.");
        }
        if (annotation is { Length: > 0 } && (document.text[start..end].IndexOfAny(['\r', '\n', '\uFFFC']) >= 0 || document.beats.Any(beat => beat.offset > start && beat.offset < end)))
            throw new InvalidOperationException("Ruby requires a continuous text range without images, line breaks or pacing changes.");
        var result = new StringBuilder(); int beatIndex = 0, runIndex = 0; double speed = 1;
        for (int index = 0; index < document.elements.Count;)
        {
            int offset = document.elements[index]; Beats(offset);
            while (runIndex + 1 < document.runs.Count && document.runs[runIndex].start + document.runs[runIndex].length <= offset) runIndex++;
            NovelTextRun run = document.runs[runIndex]; int next = index + 1;
            bool selected = offset >= start && offset < end;
            if (annotation is { Length: > 0 } && offset == start) next = first + count;
            else if (run.ruby.Length > 0) while (next < document.elements.Count && document.elements[next] < run.start + run.length) next++;
            else if (atomic.TryGetValue(offset, out var token)) while (next < document.elements.Count && document.elements[next] < token.start + token.length) next++;
            int finish = next == document.elements.Count ? document.text.Length : document.elements[next];
            string ruby = annotation is not null && selected ? annotation : run.ruby;
            NovelTextStyle style = selected && replacement is not null ? replacement : run.style;
            string content = run.icon.Length > 0 ? "[icon=" + run.icon + "]" : NovelTextMarkup.Escape(document.text[offset..finish]);
            if (ruby.Length > 0) content = "[ruby=" + ruby + "]" + content + "[/ruby]";
            result.Append(Styled(content, style, selected && speedOverride is { } localSpeed ? localSpeed : speed)); index = next;
        }
        Beats(document.text.Length);
        string rewritten = result.ToString();
        if (NovelTextMarkup.Parse(rewritten).text != document.text) throw new InvalidOperationException("Formatting changed the source text.");
        return rewritten;

        void Beats(int offset)
        {
            while (beatIndex < document.beats.Count && document.beats[beatIndex].offset <= offset)
            {
                NovelTextBeat beat = document.beats[beatIndex++]; speed = beat.speed;
                if (beat.pause > 0) result.Append("[pause=" + beat.pause.ToString("R", CultureInfo.InvariantCulture) + "]");
            }
        }
    }

    private static string Styled(
        string content,
        NovelTextStyle style,
        double speed
    ) {
        if (style.shadow != 0) content = Wrap("shadowcolor", Color(style.shadowColor), Wrap("shadow", Number(style.shadow), content));
        if (style.outline != 0) content = Wrap("outlinecolor", Color(style.outlineColor), Wrap("outline", Number(style.outline), content));
        if (style.font.Length > 0) content = Wrap("font", style.font, content);
        if (style.color is { } color) content = Wrap("color", Color(color), content);
        if (style.size != 0) content = Wrap("size", Number(style.size), content);
        if (style.italic) content = "[i]" + content + "[/i]";
        if (style.bold) content = "[b]" + content + "[/b]";
        return speed == 1 ? content : Wrap("speed", speed.ToString("R", CultureInfo.InvariantCulture), content);
    }
    private static string Wrap(string tag, string argument, string text) => "[" + tag + "=" + argument + "]" + text + "[/" + tag + "]";
    private static string Number(float number) => number.ToString("R", CultureInfo.InvariantCulture);
    private static string Color(uint color) => "#" + color.ToString("X8", CultureInfo.InvariantCulture);
}

using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Text;

namespace Inno.VisualNovel.Narrative.Reading;

/// <summary>Owns bounded NVL page composition independently of UI layout and the interpreter instruction budget.</summary>
public sealed class ReadingPageController
{
    private readonly List<ReadingPageLine> m_lines = [];
    private NovelTextDocument? m_document;
    /// <summary>Gets the full immutable page, including the active line's unrevealed content.</summary>
    public NovelTextDocument document => m_document ??= Compose(m_lines);
    /// <summary>Gets the full page-local line count.</summary>
    public int count => m_lines.Count;
    /// <summary>Clears a page explicitly without mutating reading history or progress.</summary>
    public void Clear() { m_lines.Clear(); m_document = null; }
    /// <summary>Appends a detached line while enforcing an explicit author-visible page bound.</summary>
    /// <param name="line">The resolved line and its frozen substitutions.</param>
    public void Append(ReadingPageLine line)
    {
        if (m_lines.Count >= 200) throw new InvalidOperationException("NVL page exceeds 200 lines. Begin a fresh page in the dialogue properties.");
        ReadingPageLine candidate = Copy(line); _ = Compose(m_lines.Append(candidate));
        m_lines.Add(candidate); m_document = null;
    }
    /// <summary>Captures neutral line data for save, rollback and language candidates.</summary>
    /// <returns>Detached page entries.</returns>
    public ReadingPageLine[] Capture() => m_lines.Select(Copy).ToArray();
    /// <summary>Validates all entries before replacing the active page.</summary>
    /// <param name="lines">The candidate entries.</param>
    public void Restore(ReadingPageLine[] lines)
    {
        if (lines.Length > 200) throw new InvalidOperationException("NVL page snapshot exceeds its line budget.");
        ReadingPageLine[] copy = lines.Select(Copy).ToArray();
        _ = Compose(copy);
        m_lines.Clear(); m_lines.AddRange(copy); m_document = null;
    }
    /// <summary>Creates a detached validated line without aliasing substitutions.</summary>
    /// <param name="line">The source line.</param>
    /// <returns>Independent neutral data.</returns>
    public static ReadingPageLine Copy(ReadingPageLine line)
    {
        if (line.nodeId.Length == 0 || line.markup.Length > 1_000_000 || line.speaker.Length > 100000 || line.parameters.Length > 256
            || line.parameters.Any(static parameter => string.IsNullOrWhiteSpace(parameter.id) || parameter.value.Length > 1_000_000)
            || line.parameters.Select(static parameter => parameter.id).Distinct(StringComparer.Ordinal).Count() != line.parameters.Length)
            throw new InvalidOperationException("NVL line contains invalid neutral text state.");
        _ = NovelTextMarkup.Parse(line.markup);
        return new() { nodeId = line.nodeId, markup = line.markup, speaker = line.speaker, locale = line.locale,
            parameters = line.parameters.Select(static parameter => new DialogueParameter { id = parameter.id, value = parameter.value }).ToArray() };
    }

    private static NovelTextDocument Compose(IEnumerable<ReadingPageLine> lines) => NovelTextMarkup.Parse(string.Join("\n\n", lines.Select(static line =>
        (line.speaker.Length == 0 ? string.Empty : "[b]" + NovelTextMarkup.Escape(line.speaker) + "[/b]\n") + line.markup)));
}

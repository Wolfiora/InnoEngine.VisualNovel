using System;
using System.Linq;

namespace Inno.VisualNovel.Authoring.Documents;

/// <summary>Edits paragraph boundaries in the same source string without introducing another document or undo history.</summary>
public static class ParagraphText
{
    /// <summary>Inserts an empty paragraph after a selected source line.</summary>
    /// <param name="text">The authored multiline string.</param>
    /// <param name="line">The zero-based selected paragraph.</param>
    /// <returns>The updated source string with an explicit newline.</returns>
    public static string InsertAfter(
        string text,
        int line
    ) {
        string[] lines = Lines(text);
        if (line < 0 || line >= lines.Length) throw new ArgumentOutOfRangeException(nameof(line));
        return string.Join('\n', lines.Take(line + 1).Concat([string.Empty]).Concat(lines.Skip(line + 1)));
    }
    /// <summary>Removes only an explicitly empty paragraph, preserving all nonempty authored text.</summary>
    /// <param name="text">The authored multiline string.</param>
    /// <param name="line">The empty source paragraph.</param>
    /// <returns>The source string after removing that boundary.</returns>
    public static string RemoveEmpty(
        string text,
        int line
    ) {
        string[] lines = Lines(text);
        if (line < 0 || line >= lines.Length || lines.Length == 1 || lines[line].Length > 0) throw new InvalidOperationException("Only an empty paragraph can be removed with this action.");
        return string.Join('\n', lines.Where((_, index) => index != line));
    }
    /// <summary>Gets source paragraphs with trailing empty paragraphs preserved.</summary>
    /// <param name="text">The authored text.</param>
    /// <returns>Editable lines; line endings normalize only when the caller commits an edit.</returns>
    public static string[] Lines(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
}

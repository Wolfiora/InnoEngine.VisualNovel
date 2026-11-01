using System;
using System.Collections.Generic;
using System.Text;

namespace Inno.VisualNovel.Authoring.AssetImporting;

/// <summary>Parses bounded quoted CSV and TSV exchanges independently of native asset persistence.</summary>
public static class DelimitedTextTable
{
    /// <summary>Reads complete logical records, preserving quoted separators, escaped quotes and multiline cells.</summary>
    /// <param name="text">The exchange text, optionally beginning with a Unicode byte-order mark.</param>
    /// <param name="separator">Comma or tab.</param>
    /// <returns>Caller-owned records; malformed quoting fails before producing a partial table.</returns>
    public static IReadOnlyList<string[]> Parse(
        string text,
        char separator
    ) {
        if (separator is not (',' or '\t')) throw new ArgumentException("Use comma-separated CSV or tab-separated TSV.", nameof(separator));
        if (text.Length > 16 * 1024 * 1024) throw new InvalidOperationException("Text exchange exceeds the 16 MiB preview budget.");
        var rows = new List<string[]>(); var cells = new List<string>(); var value = new StringBuilder(); bool quoted = false, closed = false;
        for (int index = text.StartsWith('\uFEFF') ? 1 : 0; index < text.Length; index++)
        {
            char ch = text[index];
            if (quoted)
            {
                if (ch != '"') value.Append(ch);
                else if (index + 1 < text.Length && text[index + 1] == '"') { value.Append('"'); index++; }
                else { quoted = false; closed = true; }
                continue;
            }
            if (ch == separator || ch is '\r' or '\n')
            {
                cells.Add(value.ToString()); value.Clear(); closed = false;
                if (ch != separator) { if (ch == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++; rows.Add(cells.ToArray()); cells.Clear(); }
                continue;
            }
            if (closed) throw new InvalidOperationException("Unexpected text after a quoted exchange cell.");
            if (ch == '"') { if (value.Length != 0) throw new InvalidOperationException("Quotes must begin an exchange cell."); quoted = true; }
            else value.Append(ch);
        }
        if (quoted) throw new InvalidOperationException("Unclosed quoted exchange cell.");
        if (value.Length > 0 || cells.Count > 0 || closed) { cells.Add(value.ToString()); rows.Add(cells.ToArray()); }
        return rows;
    }
}

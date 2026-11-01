using System;
using System.Collections.Generic;

namespace Inno.VisualNovel.Model.Text;

/// <summary>Normalizes literal and variable ranges shared by compilation, formatting and runtime interpolation.</summary>
public static class NovelTextTemplate
{
    /// <summary>Parses whole variable tokens and paired literal braces without evaluating game state.</summary>
    /// <param name="source">The bounded authored template.</param>
    /// <returns>Immutable source ranges with resolved literal brace escapes.</returns>
    public static IReadOnlyList<NovelTextToken> Parse(string source)
    {
        if (source.Length > 1_000_000) throw new FormatException("Text template exceeds its character budget.");
        var tokens = new List<NovelTextToken>(); int start = 0;
        for (int index = 0; index < source.Length; index++)
        {
            if (source[index] == '}' && index + 1 < source.Length && source[index + 1] == '}')
            {
                if (index > start) tokens.Add(new(start, index - start, source[start..index], false));
                tokens.Add(new(index, 2, "}", false)); index++; start = index + 1; continue;
            }
            if (source[index] != '{') continue;
            if (index > start) tokens.Add(new(start, index - start, source[start..index], false));
            if (index + 1 < source.Length && source[index + 1] == '{')
            { tokens.Add(new(index, 2, "{", false)); index++; start = index + 1; continue; }
            int closing = source.IndexOf('}', index + 1);
            if (closing < 0) throw new FormatException("Unclosed story interpolation at " + index + ".");
            string id = source[(index + 1)..closing];
            if (string.IsNullOrWhiteSpace(id) || id.Length > 256 || id.IndexOfAny(['{', '[', ']']) >= 0)
                throw new FormatException("Invalid variable placeholder at " + index + ". Select the whole placeholder when formatting.");
            tokens.Add(new(index, closing - index + 1, id, true)); index = closing; start = index + 1;
        }
        if (start < source.Length) tokens.Add(new(start, source.Length - start, source[start..], false));
        return tokens.AsReadOnly();
    }
}

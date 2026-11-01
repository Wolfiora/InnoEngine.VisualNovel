using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Inno.VisualNovel.Model.Text;

/// <summary>Normalizes bounded VN text markup into immutable ranges and pacing declarations.</summary>
public static class NovelTextMarkup
{
    /// <summary>Checks the shared stable key syntax for page-owned text resources.</summary>
    /// <param name="value">A candidate font or image key.</param>
    /// <returns>Whether the bounded key can be represented by a text tag.</returns>
    public static bool IsResourceKey(string value) => value.Length is > 0 and <= 128 && value.All(static c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.' or '/');
    /// <summary>Escapes arbitrary data so it cannot introduce formatting or pacing instructions.</summary>
    /// <param name="text">The literal input.</param>
    /// <returns>Markup representing exactly the supplied text.</returns>
    public static string Escape(string text) => text.Replace("[", "[[", StringComparison.Ordinal);

    /// <summary>Parses authored markup with nested styles, ruby, images, pauses and speed scopes.</summary>
    /// <param name="markup">Resolved markup with literal interpolation values already escaped.</param>
    /// <returns>A normalized immutable document.</returns>
    /// <exception cref="FormatException">Tags, nesting, numbers or resource keys are invalid.</exception>
    public static NovelTextDocument Parse(string markup)
    {
        ArgumentNullException.ThrowIfNull(markup);
        if (markup.Length > 1_000_000) throw new FormatException("Text exceeds the one-million-character budget.");
        var plain = new StringBuilder(); var runs = new List<NovelTextRun>(); var beats = new List<NovelTextBeat>();
        var stack = new Stack<(string tag, NovelTextStyle style, double speed, string ruby, int start)>();
        NovelTextStyle style = new(); double speed = 1; string ruby = string.Empty; int runStart = 0;
        for (int index = 0; index < markup.Length; index++)
        {
            if (markup[index] != '[') { plain.Append(markup[index]); continue; }
            if (index + 1 < markup.Length && markup[index + 1] == '[') { plain.Append('['); index++; continue; }
            int close = markup.IndexOf(']', index + 1);
            if (close < 0) throw new FormatException("Unclosed text tag at " + index + ". Use [[ for a literal bracket.");
            string token = markup[(index + 1)..close]; int equals = token.IndexOf('=');
            string tag = equals < 0 ? token : token[..equals]; string argument = equals < 0 ? string.Empty : token[(equals + 1)..];
            Flush();
            if (ruby.Length > 0 && tag != "/ruby") throw new FormatException("Ruby base text cannot contain nested styles or pacing tags.");
            if (tag.StartsWith("/", StringComparison.Ordinal))
            {
                if (equals >= 0 || stack.Count == 0 || stack.Peek().tag != tag[1..]) throw new FormatException("Mismatched closing text tag: " + token);
                var previous = stack.Pop();
                if (previous.tag == "ruby" && plain.Length == previous.start) throw new FormatException("Ruby requires nonempty base text.");
                style = previous.style; speed = previous.speed; ruby = previous.ruby;
                if (previous.tag == "speed") beats.Add(new(plain.Length, 0, speed));
            }
            else if (tag == "pause") beats.Add(new(plain.Length, Number(argument, 0, 60), speed));
            else if (tag == "icon")
            {
                if (ruby.Length > 0) throw new FormatException("Ruby cannot contain inline images.");
                Key(argument); runs.Add(new(plain.Length, 1, style, icon: argument)); plain.Append('\uFFFC');
            }
            else
            {
                if (stack.Count >= 32) throw new FormatException("Text style nesting exceeds 32 scopes.");
                if (ruby.Length > 0) throw new FormatException("Ruby base text cannot contain nested styles or pacing tags.");
                stack.Push((tag, style, speed, ruby, plain.Length));
                switch (tag)
                {
                    case "b" when equals < 0: style = style with { bold = true }; break;
                    case "i" when equals < 0: style = style with { italic = true }; break;
                    case "size": style = style with { size = (float)Number(argument, 4, 512) }; break;
                    case "color": style = style with { color = Color(argument) }; break;
                    case "font": Key(argument); style = style with { font = argument }; break;
                    case "outline": style = style with { outline = (float)Number(argument, 0, 8) }; break;
                    case "outlinecolor": style = style with { outlineColor = Color(argument) }; break;
                    case "shadow": style = style with { shadow = (float)Number(argument, -16, 16) }; break;
                    case "shadowcolor": style = style with { shadowColor = Color(argument) }; break;
                    case "ruby":
                        if (argument.Length is < 1 or > 256 || argument.Any(char.IsControl)) throw new FormatException("Ruby annotation requires 1–256 visible characters.");
                        ruby = argument; break;
                    case "speed": speed = Number(argument, 0.01, 100); beats.Add(new(plain.Length, 0, speed)); break;
                    default: throw new FormatException("Unknown or invalid text tag: " + token);
                }
            }
            runStart = plain.Length; index = close;
            if (runs.Count + beats.Count > 16384) throw new FormatException("Text exceeds the range and pacing budget.");
        }
        Flush();
        if (stack.Count != 0) throw new FormatException("Unclosed text style: " + stack.Peek().tag);
        string text = plain.ToString(); int[] elements = StringInfo.ParseCombiningCharacters(text);
        return new(markup, text, runs.ToArray(), beats.Select(beat => beat with { offset = Boundary(beat.offset) }).ToArray());

        int Boundary(int offset)
        {
            if (offset == text.Length || elements.Length == 0) return offset;
            int index = Array.BinarySearch(elements, offset); return index >= 0 ? offset : elements[Math.Max(0, ~index - 1)];
        }

        void Flush()
        {
            if (plain.Length > runStart) runs.Add(new(runStart, plain.Length - runStart, style, ruby));
        }
    }

    private static double Number(
        string value,
        double minimum,
        double maximum
    ) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && double.IsFinite(number) && number >= minimum && number <= maximum
        ? number : throw new FormatException("Text parameter is outside its allowed range: " + value);

    private static uint Color(string value)
    {
        if (value.Length is not (7 or 9) || value[0] != '#' || !uint.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint color))
            throw new FormatException("Text colors use #RRGGBB or #RRGGBBAA.");
        return value.Length == 7 ? color << 8 | 255u : color;
    }

    private static void Key(string value)
    {
        if (!IsResourceKey(value))
            throw new FormatException("Text resource keys use letters, digits, _, -, . or /.");
    }
}

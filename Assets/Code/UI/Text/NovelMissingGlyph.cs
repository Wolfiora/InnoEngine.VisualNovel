namespace Inno.VisualNovel.UI.Text;

/// <summary>Locates unresolved font coverage without splitting Unicode graphemes or hiding missing glyphs.</summary>
/// <param name="sourceStart">UTF-16 base-text location; ruby diagnostics point to their associated base run.</param>
/// <param name="text">The complete unresolved grapheme.</param>
/// <param name="ruby">Whether the missing grapheme belongs to ruby annotation.</param>
public sealed record NovelMissingGlyph(int sourceStart, string text, bool ruby);

namespace Inno.VisualNovel.Model.Text;

/// <summary>Describes one immutable UTF-16 range; reveal boundaries are independently grapheme-safe.</summary>
/// <param name="start">Inclusive plain-text offset.</param>
/// <param name="length">Plain-text length.</param>
/// <param name="style">The resolved inherited style.</param>
/// <param name="ruby">An annotation kept with this base range.</param>
/// <param name="icon">A page-owned image key for an object replacement character.</param>
public sealed record NovelTextRun(int start, int length, NovelTextStyle style, string ruby = "", string icon = "");

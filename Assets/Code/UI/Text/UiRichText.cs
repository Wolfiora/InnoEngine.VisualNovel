using Inno.VisualNovel.Model.Text;

namespace Inno.VisualNovel.UI.Text;

/// <summary>Pairs a full immutable line with a grapheme-safe reveal boundary to keep layout stable during typing.</summary>
/// <param name="document">The full normalized line.</param>
/// <param name="visibleGraphemes">The visible text-element count.</param>
public sealed record UiRichText(NovelTextDocument document, int visibleGraphemes = int.MaxValue);

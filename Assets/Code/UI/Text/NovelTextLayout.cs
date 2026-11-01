using System.Collections.Generic;

namespace Inno.VisualNovel.UI.Text;

/// <summary>Contains stable full-text bounds and visible glyph/image placement.</summary>
/// <param name="width">The maximum occupied line width.</param>
/// <param name="height">The full wrapped height including unrevealed content.</param>
/// <param name="glyphs">Visible glyphs in paint order.</param>
/// <param name="images">Visible inline images.</param>
/// <param name="visibleHeight">The occupied vertical extent of the revealed content.</param>
public sealed record NovelTextLayout(float width, float height, IReadOnlyList<NovelTextGlyph> glyphs, IReadOnlyList<NovelTextImage> images, float visibleHeight = 0)
{
    /// <summary>Gets immutable full-text coverage diagnostics, including currently unrevealed content.</summary>
    public IReadOnlyList<NovelMissingGlyph> missingGlyphs { get; init; } = [];
}

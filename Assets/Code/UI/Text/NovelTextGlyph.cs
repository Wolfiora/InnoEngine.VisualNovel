using InnoEngine.Text;
using Inno.VisualNovel.Model.Text;

namespace Inno.VisualNovel.UI.Text;

/// <summary>Describes a visible shaped glyph positioned relative to the text rectangle.</summary>
/// <param name="font">The borrowed imported face.</param>
/// <param name="size">Rasterization size.</param>
/// <param name="glyph">Shaper-provided identity and offsets.</param>
/// <param name="x">Horizontal pen location.</param>
/// <param name="baseline">Vertical baseline.</param>
/// <param name="style">Resolved paint decoration.</param>
public sealed record NovelTextGlyph(FontAsset font, float size, TextGlyph glyph, float x, float baseline, NovelTextStyle style);

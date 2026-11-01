using InnoEngine.Mathematics;
using System.Collections.Generic;
using InnoEngine.Rendering;
using InnoEngine.Text;
using Inno.VisualNovel.UI.Documents;
using NumericsVector2 = System.Numerics.Vector2;

namespace Inno.VisualNovel.Rendering.Frames;

/// <summary>Describes a texture source owned by a frame or imported asset.</summary>
/// <param name="key">Stable key within one rendering owner.</param>
/// <param name="image">Imported image, or null for a glyph or solid.</param>
/// <param name="glyph">Immutable glyph coverage, or null.</param>
/// <param name="bitmap">Immutable generated linear RGBA pixels, or null.</param>
public sealed record NovelTextureSource(string key, TextureAsset? image = null, GlyphBitmap? glyph = null, UI.Imaging.UiBitmap? bitmap = null);

/// <summary>Describes one alpha-composited logical quad.</summary>
/// <param name="bounds">Design-space destination.</param>
/// <param name="clip">Design-space clipping rectangle.</param>
/// <param name="color">Packed RRGGBBAA tint.</param>
/// <param name="texture">The imported image, glyph or white source.</param>
/// <param name="corners">Optional fully transformed geometry for affine stage composition.</param>
/// <param name="uv">Optional normalized source rectangle, otherwise the complete image.</param>
public sealed record NovelDrawQuad(Rect bounds, Rect clip, uint color, NovelTextureSource texture, NovelQuadCorners? corners = null, Rect? uv = null);

/// <summary>Preserves a complete affine quad without discarding shear or reflection.</summary>
/// <param name="topLeft">Upper-left transformed point.</param>
/// <param name="topRight">Upper-right transformed point.</param>
/// <param name="bottomRight">Lower-right transformed point.</param>
/// <param name="bottomLeft">Lower-left transformed point.</param>
public readonly record struct NovelQuadCorners(NumericsVector2 topLeft, NumericsVector2 topRight, NumericsVector2 bottomRight, NumericsVector2 bottomLeft);

/// <summary>Contains one immutable view submitted to the VN rendering backend.</summary>
/// <param name="width">Design canvas width.</param>
/// <param name="height">Design canvas height.</param>
/// <param name="material">The native VN material.</param>
/// <param name="quads">Ordered alpha composition commands.</param>
public sealed record NovelRenderFrame(float width, float height, MaterialAsset material, IReadOnlyList<NovelDrawQuad> quads)
{
    /// <summary>Gets the optional pure image graph required for effects and scoped transitions.</summary>
    public NovelImagePlan? image { get; init; }
    /// <summary>Gets the imported premultiplied image-processing material.</summary>
    public MaterialAsset? effectsMaterial { get; init; }
}

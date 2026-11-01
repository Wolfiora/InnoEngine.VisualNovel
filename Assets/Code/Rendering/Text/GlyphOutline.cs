using System;
using InnoEngine.Text;

namespace Inno.VisualNovel.Rendering.Text;

/// <summary>Expands glyph coverage by a circular brush while retaining its baseline bearings.</summary>
public static class GlyphOutline
{
    /// <summary>Produces padded coverage for a bounded logical outline.</summary>
    /// <param name="source">Immutable engine rasterization.</param>
    /// <param name="radius">Integer dilation radius from one through thirty-two.</param>
    /// <returns>Expanded immutable coverage with corrected bearings.</returns>
    public static GlyphBitmap Build(
        GlyphBitmap source,
        int radius
    ) {
        if (radius is < 1 or > 32) throw new ArgumentOutOfRangeException(nameof(radius));
        int width = checked(source.width + radius * 2), height = checked(source.height + radius * 2);
        var result = new byte[checked(width * height)]; ReadOnlySpan<byte> pixels = source.pixels.Span;
        for (int y = 0; y < source.height; y++)
            for (int x = 0; x < source.width; x++)
            {
                byte value = pixels[y * source.width + x]; if (value == 0) continue;
                for (int dy = -radius; dy <= radius; dy++)
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (dx * dx + dy * dy > radius * radius) continue;
                        int offset = (y + radius + dy) * width + x + radius + dx;
                        if (result[offset] < value) result[offset] = value;
                    }
            }
        return new(width, height, source.bearingX - radius, source.bearingY + radius, source.advanceX, result);
    }
}

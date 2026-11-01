using InnoEngine.Mathematics;
using System;
using System.Buffers.Binary;
using InnoEngine.Rendering;
using Inno.VisualNovel.Rendering.Frames;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Rendering.Geometry;

/// <summary>Packs logical quads into a neutral non-indexed vertex stream.</summary>
public static class QuadBatchBuilder
{
    /// <summary>Gets the packed position, UV and color layout.</summary>
    public static RenderVertexLayout layout { get; } = new([
        new(RenderVertexSemantic.Position, RenderVertexFormat.Float2),
        new(RenderVertexSemantic.TextureCoordinate0, RenderVertexFormat.Float2),
        new(RenderVertexSemantic.Color0, RenderVertexFormat.Float4)]);

    /// <summary>Packs one quad in clip coordinates with preserved alpha order.</summary>
    /// <param name="quad">The logical composition command.</param>
    /// <param name="designWidth">The positive logical canvas width.</param>
    /// <param name="designHeight">The positive logical canvas height.</param>
    /// <returns>Six interleaved vertices.</returns>
    public static byte[] Pack(
        NovelDrawQuad quad,
        float designWidth,
        float designHeight
    ) {
        var bytes = new byte[6 * 32];
        Rect b = quad.bounds, uv = quad.uv ?? new(0, 0, 1, 1);
        NovelQuadCorners points = quad.corners ?? new(new(b.x, b.y), new(b.x + b.width, b.y), new(b.x + b.width, b.y + b.height), new(b.x, b.y + b.height));
        (float x, float y, float u, float v)[] corners = [(points.topLeft.X, points.topLeft.Y, 0, 0), (points.topRight.X, points.topRight.Y, 1, 0),
            (points.bottomRight.X, points.bottomRight.Y, 1, 1), (points.topLeft.X, points.topLeft.Y, 0, 0), (points.bottomRight.X, points.bottomRight.Y, 1, 1), (points.bottomLeft.X, points.bottomLeft.Y, 0, 1)];
        for (int index = 0; index < corners.Length; index++)
        {
            var point = corners[index];
            float[] values = [point.x / designWidth * 2 - 1, 1 - point.y / designHeight * 2, uv.x + point.u * uv.width, uv.y + point.v * uv.height,
                Linear((quad.color >> 24) & 255), Linear((quad.color >> 16) & 255), Linear((quad.color >> 8) & 255), (quad.color & 255) / 255f];
            for (int component = 0; component < values.Length; component++)
                BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(index * 32 + component * 4, 4), values[component]);
        }
        return bytes;
    }

    private static float Linear(uint channel)
    {
        float value = channel / 255f;
        return value <= 0.04045f ? value / 12.92f : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
    }
}

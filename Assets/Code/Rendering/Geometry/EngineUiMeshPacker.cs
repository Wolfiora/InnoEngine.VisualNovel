using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using InnoEngine.Mathematics;
using InnoEngine.UI;

namespace Inno.VisualNovel.Rendering.Geometry;

/// <summary>Clips engine UI triangles before applying the native canvas transform.</summary>
internal static class EngineUiMeshPacker
{
    internal static byte[] Pack(
        UiMeshUpdate mesh,
        Matrix transform,
        float width,
        float height,
        Rect? clip
    ) {
        var triangles = new List<Vertex>(mesh.indices.Count);
        var polygon = new List<Vertex>(8);
        var scratch = new List<Vertex>(8);
        for (int index = 0; index < mesh.indices.Count; index += 3)
        {
            polygon.Clear();
            for (int corner = 0; corner < 3; corner++)
            {
                uint sourceIndex = mesh.indices[index + corner];
                if (sourceIndex >= mesh.vertices.Count) throw new InvalidOperationException("Engine UI mesh contains an invalid vertex index.");
                var source = mesh.vertices[(int)sourceIndex];
                byte alpha = (byte)(source.color >> 24);
                polygon.Add(new(new(source.x, source.y), new(source.u, source.v), new(
                    Linear((byte)(source.color & 255), alpha), Linear((byte)((source.color >> 8) & 255), alpha),
                    Linear((byte)((source.color >> 16) & 255), alpha), alpha / 255f)));
            }
            if (clip is { } bounds)
            {
                Cut(0, bounds.x, true); Cut(0, bounds.x + bounds.width, false);
                Cut(1, bounds.y, true); Cut(1, bounds.y + bounds.height, false);
            }
            for (int corner = 1; corner + 1 < polygon.Count; corner++)
            {
                if (triangles.Count >= 3000000) throw new InvalidOperationException("Clipped engine UI geometry exceeds the surface budget.");
                triangles.Add(polygon[0]); triangles.Add(polygon[corner]); triangles.Add(polygon[corner + 1]);
            }
        }
        byte[] bytes = new byte[checked(triangles.Count * 32)];
        for (int index = 0; index < triangles.Count; index++)
        {
            var vertex = triangles[index]; var point = Vector2.Transform(vertex.position, transform);
            float[] values = [point.x / width * 2 - 1, 1 - point.y / height * 2, vertex.uv.x, vertex.uv.y,
                vertex.color.x, vertex.color.y, vertex.color.z, vertex.color.w];
            for (int field = 0; field < values.Length; field++)
            {
                if (!float.IsFinite(values[field])) throw new InvalidOperationException("Engine UI geometry must be finite.");
                BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(index * 32 + field * 4, 4), values[field]);
            }
        }
        return bytes;

        void Cut(
            int axis,
            float limit,
            bool keepGreater
        ) {
            if (polygon.Count == 0) return;
            scratch.Clear();
            var previous = polygon[^1]; float previousDistance = Distance(previous);
            foreach (var current in polygon)
            {
                float distance = Distance(current);
                if ((distance >= 0) != (previousDistance >= 0))
                {
                    float fraction = previousDistance / (previousDistance - distance);
                    scratch.Add(new(previous.position + (current.position - previous.position) * fraction,
                        previous.uv + (current.uv - previous.uv) * fraction,
                        previous.color + (current.color - previous.color) * fraction));
                }
                if (distance >= 0) scratch.Add(current);
                previous = current; previousDistance = distance;
            }
            (polygon, scratch) = (scratch, polygon);
            float Distance(Vertex vertex) => ((axis == 0 ? vertex.position.x : vertex.position.y) - limit) * (keepGreater ? 1 : -1);
        }
    }

    internal static float Linear(
        byte premultiplied,
        byte alpha
    ) {
        float value = alpha == 0 ? 0 : Math.Min(1, premultiplied / (float)alpha);
        return value <= .04045f ? value / 12.92f : MathF.Pow((value + .055f) / 1.055f, 2.4f);
    }
    private readonly record struct Vertex(Vector2 position, Vector2 uv, Vector4 color);
}

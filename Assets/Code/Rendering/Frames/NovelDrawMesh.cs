using System;
using InnoEngine.Mathematics;

namespace Inno.VisualNovel.Rendering.Frames;

/// <summary>Contains an immutable non-indexed surface vertex stream, retaining arbitrary engine UI triangle geometry.</summary>
public sealed class NovelDrawMesh
{
    private readonly byte[] m_vertices;
    /// <summary>Creates a detached triangle draw using the VN surface vertex layout.</summary>
    /// <param name="vertices">Clip-space position, UV and linear straight RGBA, eight floats per vertex.</param>
    /// <param name="clip">Output-design clipping rectangle.</param>
    /// <param name="texture">Immutable imported or generated image data.</param>
    public NovelDrawMesh(
        ReadOnlySpan<byte> vertices,
        Rect clip,
        NovelTextureSource texture
    ) {
        if (vertices.Length == 0 || vertices.Length % 96 != 0) throw new ArgumentException("A surface mesh requires complete triangles.", nameof(vertices));
        m_vertices = vertices.ToArray(); this.clip = clip; this.texture = texture ?? throw new ArgumentNullException(nameof(texture));
    }
    /// <summary>Gets the detached packed surface vertices.</summary>
    public ReadOnlyMemory<byte> vertices => m_vertices;
    /// <summary>Gets the non-indexed vertex count.</summary>
    public int vertexCount => m_vertices.Length / 32;
    /// <summary>Gets the clipping rectangle in output design pixels.</summary>
    public Rect clip { get; }
    /// <summary>Gets the immutable texture dependency.</summary>
    public NovelTextureSource texture { get; }
}

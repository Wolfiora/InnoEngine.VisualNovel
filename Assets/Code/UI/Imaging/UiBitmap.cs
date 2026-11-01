using System;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Inno.VisualNovel.UI.Imaging;

/// <summary>Owns immutable straight-alpha linear RGBA pixels for generated UI imagery such as save thumbnails.</summary>
public sealed class UiBitmap
{
    private readonly byte[] m_pixels;
    /// <summary>Copies a bounded image independently of its producer's mutable buffers.</summary>
    /// <param name="width">Positive pixel width, at most 4096.</param>
    /// <param name="height">Positive pixel height, at most 4096.</param>
    /// <param name="pixels">Exactly four linear RGBA bytes per pixel, in top-to-bottom order.</param>
    public UiBitmap(
        int width,
        int height,
        ReadOnlySpan<byte> pixels
    ) {
        if (width is < 1 or > 4096 || height is < 1 or > 4096 || (long)width * height > 4194304 || pixels.Length != (long)width * height * 4)
            throw new ArgumentException("UI raster images require bounded dimensions and complete RGBA pixels.");
        this.width = width; this.height = height; m_pixels = pixels.ToArray();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> dimensions = stackalloc byte[8]; BinaryPrimitives.WriteInt32LittleEndian(dimensions, width); BinaryPrimitives.WriteInt32LittleEndian(dimensions[4..], height);
        hash.AppendData(dimensions); hash.AppendData(m_pixels); contentKey = Convert.ToHexString(hash.GetHashAndReset());
    }
    /// <summary>Gets the immutable pixel width.</summary>
    public int width { get; }
    /// <summary>Gets the immutable pixel height.</summary>
    public int height { get; }
    /// <summary>Gets top-to-bottom linear straight-alpha pixels.</summary>
    public ReadOnlyMemory<byte> pixels => m_pixels;
    /// <summary>Gets the size-sensitive content identity used by the generation-owned texture cache.</summary>
    public string contentKey { get; }
}

using System;
using InnoEngine.Rendering;

namespace Inno.VisualNovel.Rendering.Capture;

/// <summary>Separates the renderable image from its readback-only transfer destination.</summary>
/// <param name="image">The raster target with explicit copy-source usage.</param>
/// <param name="readback">The single-sampled transfer resource, never a raster attachment.</param>
public sealed record NovelCaptureTargets(RenderTextureDescriptor image, RenderTextureDescriptor readback)
{
    /// <summary>Creates compatible bounded descriptors using only neutral rendering capabilities.</summary>
    /// <param name="width">Positive width, at most 2048.</param>
    /// <param name="height">Positive height, at most 2048.</param>
    /// <returns>Matching RGBA8 images suitable for explicit draw, copy and later readback.</returns>
    public static NovelCaptureTargets Create(
        int width,
        int height
    ) {
        if (width is < 1 or > 2048 || height is < 1 or > 2048 || (long)width * height > 2097152) throw new ArgumentOutOfRangeException(nameof(width));
        return new(new(width, height, RenderTextureFormat.RGBA8, RenderTextureUsage.ColorAttachment | RenderTextureUsage.Sampled | RenderTextureUsage.CopySource),
            new(width, height, RenderTextureFormat.RGBA8, RenderTextureUsage.CopyDestination | RenderTextureUsage.Readback));
    }
}

using System;
using System.IO;
using InnoEngine.Rendering;
using Inno.VisualNovel.UI.Imaging;

namespace Inno.VisualNovel.Rendering.Capture;

/// <summary>Normalizes neutral RGBA readback pitch, origin and premultiplied alpha before standard PNG encoding.</summary>
public static class NovelReadbackImage
{
    /// <summary>Builds a top-to-bottom straight-alpha linear image from a completed RGBA8 render target.</summary>
    /// <param name="readback">The immutable completed texture readback.</param>
    /// <param name="originBottomLeft">Whether source row zero is the bottom of the image.</param>
    /// <returns>A bounded independent CPU image.</returns>
    public static UiBitmap Normalize(
        RenderTextureReadbackResult readback,
        bool originBottomLeft
    ) {
        int width = readback.descriptor.width >> readback.mipLevel, height = readback.descriptor.height >> readback.mipLevel;
        width = Math.Max(1, width); height = Math.Max(1, height);
        if (readback.descriptor.format != RenderTextureFormat.RGBA8 || readback.descriptor.arrayLayers != 1 || width > 2048 || height > 2048
            || (long)width * height > 2097152 || readback.rowPitch < width * 4 || (long)readback.rowPitch * height > readback.data.Length)
            throw new InvalidDataException("Screenshot readback has an unsupported format, layer count or row pitch.");
        var pixels = new byte[width * height * 4]; var source = readback.data.Span;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int input = (originBottomLeft ? height - y - 1 : y) * readback.rowPitch + x * 4, output = (y * width + x) * 4;
                byte alpha = source[input + 3]; pixels[output + 3] = alpha;
                for (int channel = 0; channel < 3; channel++) pixels[output + channel] = alpha == 0 ? (byte)0 : (byte)Math.Clamp((source[input + channel] * 255 + alpha / 2) / alpha, 0, 255);
            }
        return new(width, height, pixels);
    }
}

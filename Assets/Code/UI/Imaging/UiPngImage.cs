using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Inno.VisualNovel.UI.Imaging;

/// <summary>Encodes and decodes bounded non-interlaced RGBA8 PNG UI snapshots without an asset database or device.</summary>
public static class UiPngImage
{
    private static readonly byte[] S_signature = [137, 80, 78, 71, 13, 10, 26, 10];
    /// <summary>Writes a standard sRGB PNG from linear straight-alpha UI pixels.</summary>
    /// <param name="image">The independently owned linear source image.</param>
    /// <returns>A complete PNG image, independent of native game-state serialization.</returns>
    public static byte[] Encode(UiBitmap image)
    {
        using var output = new MemoryStream(); output.Write(S_signature);
        Span<byte> header = stackalloc byte[13]; header.Clear(); BinaryPrimitives.WriteInt32BigEndian(header, image.width); BinaryPrimitives.WriteInt32BigEndian(header[4..], image.height); header[8] = 8; header[9] = 6;
        Chunk(output, "IHDR", header); Chunk(output, "sRGB", [0]);
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, true))
        {
            var row = new byte[image.width * 4 + 1]; var pixels = image.pixels.Span;
            for (int y = 0; y < image.height; y++)
            {
                for (int x = 0; x < image.width * 4; x++) row[x + 1] = x % 4 == 3 ? pixels[y * image.width * 4 + x] : Srgb(pixels[y * image.width * 4 + x]);
                zlib.Write(row);
            }
        }
        Chunk(output, "IDAT", compressed.ToArray()); Chunk(output, "IEND", []);
        if (output.Length > 8388608) throw new InvalidDataException("PNG UI image exceeds the 8 MiB budget.");
        return output.ToArray();
    }
    /// <summary>Validates standard chunk CRCs and all five PNG row filters before returning linear pixels.</summary>
    /// <param name="png">A complete non-interlaced RGBA8 image, limited to 8 MiB.</param>
    /// <returns>An independent linear straight-alpha image.</returns>
    public static UiBitmap Decode(ReadOnlySpan<byte> png)
    {
        if (png.Length is < 45 or > 8388608 || !png[..8].SequenceEqual(S_signature)) throw new InvalidDataException("Invalid or oversized PNG thumbnail.");
        int offset = 8, width = 0, height = 0; bool ended = false, dataStarted = false, dataEnded = false;
        using var compressed = new MemoryStream();
        while (offset < png.Length)
        {
            if (png.Length - offset < 12) throw new InvalidDataException("PNG chunk is truncated.");
            int length = BinaryPrimitives.ReadInt32BigEndian(png[offset..]);
            if (length < 0 || length > png.Length - offset - 12) throw new InvalidDataException("PNG chunk length is invalid.");
            var type = png.Slice(offset + 4, 4); var data = png.Slice(offset + 8, length);
            if (Crc(png.Slice(offset + 4, length + 4)) != BinaryPrimitives.ReadUInt32BigEndian(png[(offset + 8 + length)..])) throw new InvalidDataException("PNG chunk CRC mismatch.");
            string name = Encoding.ASCII.GetString(type);
            if (name == "IHDR")
            {
                if (offset != 8 || length != 13) throw new InvalidDataException("PNG requires one leading IHDR.");
                width = BinaryPrimitives.ReadInt32BigEndian(data); height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                if (width is < 1 or > 4096 || height is < 1 or > 4096 || (long)width * height > 4194304 || data[8] != 8 || data[9] != 6 || data[10] != 0 || data[11] != 0 || data[12] != 0)
                    throw new InvalidDataException("UI snapshots support bounded non-interlaced RGBA8 PNG only.");
            }
            else if (name == "IDAT")
            {
                if (width == 0 || dataEnded) throw new InvalidDataException("PNG image data is out of order.");
                dataStarted = true; compressed.Write(data);
            }
            else if (name == "IEND")
            {
                if (!dataStarted || length != 0 || offset + 12 != png.Length) throw new InvalidDataException("PNG ending is invalid.");
                ended = true;
            }
            else
            {
                if (dataStarted) dataEnded = true;
                if ((type[0] & 32) == 0) throw new InvalidDataException("Unsupported critical PNG chunk: " + name);
                if (name == "sRGB" && (length != 1 || data[0] > 3)) throw new InvalidDataException("PNG color intent is invalid.");
            }
            offset += length + 12;
        }
        if (!ended) throw new InvalidDataException("PNG has no complete ending.");
        int stride = checked(width * 4); var filtered = new byte[checked((stride + 1) * height)]; compressed.Position = 0;
        using (var zlib = new ZLibStream(compressed, CompressionMode.Decompress, true))
        { zlib.ReadExactly(filtered); if (zlib.ReadByte() != -1) throw new InvalidDataException("PNG decompression exceeded its declared dimensions."); }
        var pixels = new byte[checked(stride * height)];
        for (int y = 0; y < height; y++)
        {
            byte filter = filtered[y * (stride + 1)]; if (filter > 4) throw new InvalidDataException("PNG row filter is invalid.");
            for (int x = 0; x < stride; x++)
            {
                int left = x < 4 ? 0 : pixels[y * stride + x - 4], up = y == 0 ? 0 : pixels[(y - 1) * stride + x], corner = y == 0 || x < 4 ? 0 : pixels[(y - 1) * stride + x - 4];
                int predictor = filter switch { 1 => left, 2 => up, 3 => (left + up) / 2, 4 => Paeth(left, up, corner), _ => 0 };
                pixels[y * stride + x] = unchecked((byte)(filtered[y * (stride + 1) + x + 1] + predictor));
            }
        }
        for (int index = 0; index < pixels.Length; index++) if (index % 4 != 3) pixels[index] = Linear(pixels[index]);
        return new(width, height, pixels);
    }
    private static void Chunk(
        Stream stream,
        string type,
        ReadOnlySpan<byte> data
    ) {
        Span<byte> length = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(length, data.Length); stream.Write(length);
        var content = new byte[data.Length + 4]; Encoding.ASCII.GetBytes(type, content); data.CopyTo(content.AsSpan(4)); stream.Write(content);
        BinaryPrimitives.WriteUInt32BigEndian(length, Crc(content)); stream.Write(length);
    }
    private static uint Crc(ReadOnlySpan<byte> data)
    {
        uint crc = uint.MaxValue;
        foreach (byte value in data) { crc ^= value; for (int bit = 0; bit < 8; bit++) crc = crc >> 1 ^ (crc % 2 == 0 ? 0u : 0xEDB88320u); }
        return ~crc;
    }
    private static int Paeth(
        int left,
        int up,
        int corner
    ) {
        int prediction = left + up - corner, a = Math.Abs(prediction - left), b = Math.Abs(prediction - up), c = Math.Abs(prediction - corner);
        return a <= b && a <= c ? left : b <= c ? up : corner;
    }
    private static byte Srgb(byte value)
    {
        double linear = value / 255.0; return (byte)Math.Clamp(Math.Round((linear <= .0031308 ? linear * 12.92 : 1.055 * Math.Pow(linear, 1 / 2.4) - .055) * 255), 0, 255);
    }
    private static byte Linear(byte value)
    {
        double srgb = value / 255.0; return (byte)Math.Clamp(Math.Round((srgb <= .04045 ? srgb / 12.92 : Math.Pow((srgb + .055) / 1.055, 2.4)) * 255), 0, 255);
    }
}

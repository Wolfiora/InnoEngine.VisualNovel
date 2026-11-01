using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class OriginalParticleSprites
{
    internal static readonly string[] names = ["Rain", "Snow", "Dust", "Petals", "Fireflies", "LightPoints", "Bubbles", "Confetti"];
    internal static void Write(string root)
    {
        string directory = Path.Combine(root, "Assets", "BuiltIn", "Particles", "Sprites"); Directory.CreateDirectory(directory);
        foreach (string name in names)
        {
            string path = Path.Combine(directory, name + ".png");
            if (File.Exists(path)) continue;
            const int size = 64; byte[] scanlines = new byte[(size * 4 + 1) * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    double alpha = 0;
                    for (int sy = 0; sy < 4; sy++) for (int sx = 0; sx < 4; sx++)
                        alpha += Shape(name, (x + (sx + 0.5) / 4) / size * 2 - 1, (y + (sy + 0.5) / 4) / size * 2 - 1);
                    int offset = y * (size * 4 + 1) + 1 + x * 4;
                    scanlines[offset] = scanlines[offset + 1] = scanlines[offset + 2] = 255; scanlines[offset + 3] = (byte)Math.Round(Math.Clamp(alpha / 16, 0, 1) * 255);
                }
            using var output = File.Create(path); output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            byte[] header = new byte[13]; BinaryPrimitives.WriteInt32BigEndian(header, size); BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), size); header[8] = 8; header[9] = 6;
            Chunk(output, "IHDR", header);
            using var compressed = new MemoryStream();
            using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, true)) zlib.Write(scanlines);
            Chunk(output, "IDAT", compressed.ToArray()); Chunk(output, "IEND", []);
        }
    }
    private static double Shape(string name, double x, double y)
    {
        double radius = Math.Sqrt(x * x + y * y);
        return name switch
        {
            "Rain" => Math.Abs(x) < 0.24 && Math.Abs(y) < 0.92 ? 1 : 0,
            "Snow" => Math.Abs(y) < 0.82 && Math.Abs(x) < 0.065 || Math.Abs(0.5 * x + 0.866 * y) < 0.065 && radius < 0.82
                || Math.Abs(0.5 * x - 0.866 * y) < 0.065 && radius < 0.82 ? 1 : 0,
            "Dust" => Math.Max(0, 1 - radius / 0.85),
            "Petals" => (x + 0.12) * (x + 0.12) / 0.36 + y * y / 0.7 < 1 && x + y * 0.3 < 0.55 ? 1 : 0,
            "Fireflies" => radius < 0.85 ? Math.Exp(-radius * radius * 8) : 0,
            "LightPoints" => radius < 0.9 ? Math.Exp(-radius * radius * 12) : 0,
            "Bubbles" => Math.Abs(radius - 0.68) < 0.05 ? 0.9 : (x + 0.22) * (x + 0.22) + (y + 0.28) * (y + 0.28) < 0.012 ? 1 : 0,
            "Confetti" => Math.Abs(x) < 0.7 && Math.Abs(y) < 0.4 ? 1 : 0,
            _ => throw new ArgumentException("Unknown original particle sprite.", nameof(name))
        };
    }
    private static void Chunk(Stream output, string kind, byte[] data)
    {
        Span<byte> number = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(number, data.Length); output.Write(number);
        byte[] tag = Encoding.ASCII.GetBytes(kind); output.Write(tag); output.Write(data);
        uint crc = uint.MaxValue;
        foreach (byte value in tag) Update(value);
        foreach (byte value in data) Update(value);
        BinaryPrimitives.WriteUInt32BigEndian(number, ~crc); output.Write(number);
        void Update(byte value)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++) crc = crc >> 1 ^ ((crc & 1) != 0 ? 0xEDB88320u : 0);
        }
    }
}

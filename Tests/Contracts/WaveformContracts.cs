using System;
using System.IO;
using System.Linq;
using Inno.VisualNovel.Authoring.VoiceManagement;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class WaveformContracts
{
    internal static void SampleFormatsAndLosslessCrop()
    {
        byte[] source = Wave(1, 2, 8, 16, Samples(writer =>
        {
            foreach (short value in new short[] { short.MinValue, short.MaxValue, 0, 1000, 10000, -10000, 2000, 0, 20000, -20000, 0, 0 }) writer.Write(value);
        }));
        byte[] original = source.ToArray();
        var content = Read(source); source[44] = 0;
        var peaks = content.BuildPeaks(3);
        Check(content.frameCount == 6 && content.duration == 0.75 && peaks.Count == 3 && peaks[0].minimum == -1 && peaks[0].maximum > 0.999,
            "Waveform analysis lost a short transient, signed PCM value or interleaved channel.");
        var crop = Read(content.Trim(0.125, 0.625)); var croppedPeaks = crop.BuildPeaks(4);
        Check(crop.frameCount == 4 && crop.channels == 2 && crop.sampleRate == 8 && croppedPeaks[0].minimum == 0
            && Math.Abs(croppedPeaks[0].maximum - 1000f / 32768) < 1e-7 && croppedPeaks[3].minimum < -0.6,
            "Half-open frame-aligned crop changed samples, channel order or clip bounds.");
        Check(content.Trim(0, content.duration).AsSpan().SequenceEqual(original), "Cropping the full PCM interval changed sample bytes or format.");
        byte[] unsigned = Wave(1, 1, 8, 8, new byte[] { 0, 128, 255 }, junk: true); var eight = Read(unsigned);
        Check(eight.BuildPeaks(3)[0].minimum == -1 && eight.BuildPeaks(3)[1].maximum == 0 && Read(eight.Trim(0, 0.125)).frameCount == 1,
            "Unsigned 8-bit PCM or odd-sized padded chunks were interpreted incorrectly.");
        var twentyFour = Read(Wave(1, 1, 8, 24, new byte[] { 0, 0, 128, 255, 255, 127 }));
        Check(twentyFour.BuildPeaks(2)[0].minimum == -1 && twentyFour.BuildPeaks(2)[1].maximum > 0.999, "24-bit signed sample extension was incorrect.");
        var thirtyTwo = Read(Wave(1, 1, 8, 32, Samples(writer => { writer.Write(int.MinValue); writer.Write(int.MaxValue); })));
        Check(thirtyTwo.BuildPeaks(2)[0].minimum == -1 && thirtyTwo.BuildPeaks(2)[1].maximum == 1, "32-bit PCM normalization lost endpoints.");
        foreach (int bits in new[] { 32, 64 })
        {
            var floating = Read(Wave(3, 1, 8, bits, Samples(writer => { if (bits == 32) { writer.Write(-0.5f); writer.Write(0.75f); } else { writer.Write(-0.5); writer.Write(0.75); } })));
            Check(floating.BuildPeaks(2)[0].minimum == -0.5 && Read(floating.Trim(0, 0.125)).BuildPeaks(1)[0].maximum == -0.5,
                "IEEE float cropping or fact metadata changed sample values.");
        }
        var extensible = Read(Wave(0xfffe, 2, 8, 16, Samples(writer => { writer.Write(short.MinValue); writer.Write(short.MaxValue); }), extensible: true));
        Check(Read(extensible.Trim(0, extensible.duration)).BuildPeaks(1)[0].minimum == -1, "Extensible PCM channel layout was not retained.");
        Check(Read(original).BuildPeaks(8192).Count == 6, "Waveform resolution invented empty sample buckets.");
    }

    internal static void InvalidBoundaries()
    {
        byte[] source = Wave(1, 1, 8, 16, Samples(writer => { writer.Write((short)0); writer.Write((short)1000); }));
        var content = Read(source);
        Reject(() => content.Trim(double.NaN, 0.1)); Reject(() => content.Trim(-1, 0.1)); Reject(() => content.Trim(0.1, 0.1));
        Reject(() => content.Trim(0, 2)); Reject(() => content.Trim(0, 0.001)); Reject(() => content.BuildPeaks(0));
        byte[] truncated = source[..^1]; Reject(() => Read(truncated));
        byte[] compressed = source.ToArray(); compressed[20] = 2; Reject(() => Read(compressed));
        byte[] alignment = source.ToArray(); alignment[32] = 3; Reject(() => Read(alignment));
        var invalidFloat = Read(Wave(3, 1, 8, 32, Samples(writer => writer.Write(float.NaN)))); Reject(() => invalidFloat.BuildPeaks());
        byte[] badExtensible = Wave(0xfffe, 1, 8, 16, new byte[] { 0, 0 }, extensible: true); badExtensible[44] = 0x42; Reject(() => Read(badExtensible));
    }
    private static WaveAudioContent Read(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, false); return WaveAudioContent.Read(stream);
    }
    private static byte[] Samples(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream); write(writer); writer.Flush(); return stream.ToArray();
    }
    private static byte[] Wave(
        ushort tag,
        ushort channels,
        int rate,
        int bits,
        byte[] samples,
        bool junk = false,
        bool extensible = false
    ) {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8); writer.Write(0); writer.Write("WAVE"u8);
        if (junk) { writer.Write("JUNK"u8); writer.Write(1); writer.Write((ushort)42); }
        writer.Write("fmt "u8); writer.Write(extensible ? 40 : 16); writer.Write(tag); writer.Write(channels); writer.Write(rate);
        int align = channels * bits / 8; writer.Write(rate * align); writer.Write((ushort)align); writer.Write((ushort)bits);
        if (extensible) { writer.Write((ushort)22); writer.Write((ushort)bits); writer.Write(3); writer.Write(new Guid("00000001-0000-0010-8000-00aa00389b71").ToByteArray()); }
        writer.Write("data"u8); writer.Write(samples.Length); writer.Write(samples); if ((samples.Length & 1) != 0) writer.Write((byte)0);
        writer.Flush(); stream.Position = 4; writer.Write((int)stream.Length - 8); writer.Flush(); return stream.ToArray();
    }
    private static void Reject(Action action)
    {
        try { action(); } catch (Exception error) when (error is ArgumentException or InvalidDataException or InvalidOperationException) { return; }
        throw new InvalidOperationException("Malformed audio or an invalid crop boundary was accepted.");
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}

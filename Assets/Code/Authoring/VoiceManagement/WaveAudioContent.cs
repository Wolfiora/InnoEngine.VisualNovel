using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace Inno.VisualNovel.Authoring.VoiceManagement;

/// <summary>Represents channel-combined minimum and maximum samples over one time bucket.</summary>
/// <param name="minimum">The lowest normalized sample.</param>
/// <param name="maximum">The highest normalized sample.</param>
public readonly record struct WaveformPeak(float minimum, float maximum);

/// <summary>Reads bounded PCM/IEEE-float RIFF wave content for offline authoring; it never opens an audio device.</summary>
public sealed class WaveAudioContent
{
    private readonly byte[] m_format;
    private readonly byte[] m_samples;
    private readonly bool m_float;
    private readonly int m_bits;
    private readonly int m_alignment;
    private WaveAudioContent(
        byte[] format,
        byte[] samples,
        bool floating,
        int bits,
        int alignment,
        int channels,
        int rate
    ) {
        m_format = format; m_samples = samples; m_float = floating; m_bits = bits; m_alignment = alignment;
        this.channels = channels; sampleRate = rate;
    }
    /// <summary>Gets the encoded number of interleaved channels.</summary>
    public int channels { get; }
    /// <summary>Gets sample frames per second.</summary>
    public int sampleRate { get; }
    /// <summary>Gets the number of complete interleaved frames.</summary>
    public int frameCount => m_samples.Length / m_alignment;
    /// <summary>Gets the exact duration derived from frame count.</summary>
    public double duration => (double)frameCount / sampleRate;
    /// <summary>Reads complete standard RIFF WAVE data with a 64 MiB source limit.</summary>
    /// <param name="stream">The native audio-data artifact stream; ownership remains with the caller.</param>
    /// <returns>Independent immutable sample and format data.</returns>
    public static WaveAudioContent Read(Stream stream)
    {
        const int limit = 64 * 1024 * 1024;
        using var input = new MemoryStream(); byte[] buffer = new byte[32768]; int read;
        while ((read = stream.Read(buffer)) > 0)
        {
            if (input.Length + read > limit) throw new InvalidDataException("Waveform sources are limited to 64 MiB; split long recordings before importing.");
            input.Write(buffer, 0, read);
        }
        byte[] source = input.ToArray();
        if (source.Length < 12 || !source.AsSpan(0, 4).SequenceEqual("RIFF"u8) || !source.AsSpan(8, 4).SequenceEqual("WAVE"u8)
            || BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(4, 4)) != source.Length - 8)
            throw new InvalidDataException("Waveform editing requires a complete RIFF WAVE artifact.");
        byte[]? format = null, samples = null;
        for (int offset = 12; offset < source.Length;)
        {
            if (source.Length - offset < 8) throw new InvalidDataException("The WAVE chunk header is truncated.");
            ReadOnlySpan<byte> id = source.AsSpan(offset, 4); uint count = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(offset + 4, 4));
            long end = offset + 8L + count + (count & 1);
            if (end > source.Length || count > int.MaxValue) throw new InvalidDataException("The WAVE chunk exceeds its RIFF boundary.");
            if (id.SequenceEqual("fmt "u8))
            {
                if (format is not null || count is < 16 or > 65536) throw new InvalidDataException("The WAVE format chunk is missing or duplicated.");
                format = source.AsSpan(offset + 8, (int)count).ToArray();
            }
            else if (id.SequenceEqual("data"u8))
            {
                if (samples is not null) throw new InvalidDataException("Multiple WAVE data chunks are not supported by the authoring tool.");
                samples = source.AsSpan(offset + 8, (int)count).ToArray();
            }
            offset = (int)end;
        }
        if (format is null || samples is null) throw new InvalidDataException("The WAVE artifact needs format and sample chunks.");
        ushort tag = BinaryPrimitives.ReadUInt16LittleEndian(format), encoding = tag;
        int channels = BinaryPrimitives.ReadUInt16LittleEndian(format.AsSpan(2)), rate = BinaryPrimitives.ReadInt32LittleEndian(format.AsSpan(4));
        int alignment = BinaryPrimitives.ReadUInt16LittleEndian(format.AsSpan(12)), bits = BinaryPrimitives.ReadUInt16LittleEndian(format.AsSpan(14));
        if (tag == 0xFFFE)
        {
            if (format.Length < 40 || BinaryPrimitives.ReadUInt16LittleEndian(format.AsSpan(16)) < 22) throw new InvalidDataException("The extensible WAVE format is truncated.");
            Guid subtype = new(format.AsSpan(24, 16));
            encoding = subtype == new Guid("00000001-0000-0010-8000-00aa00389b71") ? (ushort)1
                : subtype == new Guid("00000003-0000-0010-8000-00aa00389b71") ? (ushort)3 : (ushort)0;
            int valid = BinaryPrimitives.ReadUInt16LittleEndian(format.AsSpan(18));
            if (valid <= 0 || valid > bits || encoding == 3 && valid != bits) throw new InvalidDataException("The extensible sample precision is invalid.");
        }
        if (encoding is not (1 or 3) || encoding == 1 && bits is not (8 or 16 or 24 or 32) || encoding == 3 && bits is not (32 or 64))
            throw new InvalidDataException("Waveform editing supports PCM 8/16/24/32-bit and IEEE-float 32/64-bit WAVE. Compressed sources remain playable through the engine but need external conversion for editing.");
        if (channels is < 1 or > 32 || rate is < 1 or > 768000 || alignment != channels * (bits / 8)
            || BinaryPrimitives.ReadUInt32LittleEndian(format.AsSpan(8)) != (long)rate * alignment || samples.Length == 0 || samples.Length % alignment != 0)
            throw new InvalidDataException("The WAVE channel, frame alignment, rate or sample count is invalid.");
        return new(format, samples, encoding == 3, bits, alignment, channels, rate);
    }
    /// <summary>Builds bounded time buckets across all channels, preserving short transients.</summary>
    /// <param name="buckets">The desired resolution from 1 to 8192, capped by available frames.</param>
    /// <returns>Immutable minimum/maximum peaks in chronological order.</returns>
    public IReadOnlyList<WaveformPeak> BuildPeaks(int buckets = 1024)
    {
        if (buckets is < 1 or > 8192) throw new ArgumentOutOfRangeException(nameof(buckets));
        int count = Math.Min(buckets, frameCount); var peaks = new WaveformPeak[count];
        for (int index = 0; index < count; index++)
        {
            int start = (int)((long)index * frameCount / count), end = (int)((long)(index + 1) * frameCount / count);
            float low = 1, high = -1;
            for (int frame = start; frame < end; frame++)
                for (int channel = 0; channel < channels; channel++)
                {
                    float sample = Sample(frame * m_alignment + channel * (m_bits / 8));
                    low = Math.Min(low, sample); high = Math.Max(high, sample);
                }
            peaks[index] = new(low, high);
        }
        return Array.AsReadOnly(peaks);
    }
    /// <summary>Creates a lossless independent WAVE containing a frame-aligned half-open time interval.</summary>
    /// <param name="start">The inclusive start in seconds.</param>
    /// <param name="end">The exclusive end in seconds.</param>
    /// <returns>Standard audio asset bytes retaining the original sample format and channel layout.</returns>
    public byte[] Trim(
        double start,
        double end
    ) {
        if (!double.IsFinite(start) || !double.IsFinite(end) || start < 0 || end > duration || end <= start) throw new ArgumentOutOfRangeException(nameof(start), "Choose a nonempty interval within the clip.");
        int first = (int)Math.Round(start * sampleRate, MidpointRounding.AwayFromZero), last = (int)Math.Round(end * sampleRate, MidpointRounding.AwayFromZero);
        if (first < 0 || last > frameCount || last <= first) throw new InvalidOperationException("The selected range must contain at least one complete sample frame.");
        using var output = new MemoryStream(); using var writer = new BinaryWriter(output);
        writer.Write("RIFF"u8); writer.Write(0); writer.Write("WAVE"u8);
        int formatLength = m_format.Length + (m_float && m_format.Length == 16 ? 2 : 0);
        writer.Write("fmt "u8); writer.Write(formatLength); writer.Write(m_format);
        if (formatLength != m_format.Length) writer.Write((ushort)0);
        if ((formatLength & 1) != 0) writer.Write((byte)0);
        if (BinaryPrimitives.ReadUInt16LittleEndian(m_format) != 1) { writer.Write("fact"u8); writer.Write(4); writer.Write(last - first); }
        int bytes = (last - first) * m_alignment;
        writer.Write("data"u8); writer.Write(bytes); writer.Write(m_samples.AsSpan(first * m_alignment, bytes)); if ((bytes & 1) != 0) writer.Write((byte)0);
        writer.Flush(); output.Position = 4; writer.Write(checked((int)output.Length - 8)); writer.Flush(); return output.ToArray();
    }
    private float Sample(int offset)
    {
        ReadOnlySpan<byte> data = m_samples.AsSpan(offset);
        double value = m_float ? m_bits == 32 ? BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(data))
            : BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(data))
            : m_bits switch
            {
                8 => (data[0] - 128) / 128.0,
                16 => BinaryPrimitives.ReadInt16LittleEndian(data) / 32768.0,
                24 => ((data[0] | data[1] << 8 | data[2] << 16) << 8 >> 8) / 8388608.0,
                _ => BinaryPrimitives.ReadInt32LittleEndian(data) / 2147483648.0
            };
        if (!double.IsFinite(value)) throw new InvalidDataException("Waveform samples contain non-finite floating-point values.");
        return (float)Math.Clamp(value, -1, 1);
    }
}

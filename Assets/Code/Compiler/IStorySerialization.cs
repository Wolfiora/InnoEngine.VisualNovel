using System;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Compiler;

/// <summary>Provides native serialization for closed, reference-free compiler documents.</summary>
public interface IStorySerialization
{
    /// <summary>Captures a closed compiler value using the engine serializer.</summary>
    /// <typeparam name="T">The serializable value contract.</typeparam>
    /// <param name="value">The complete reference-free value.</param>
    /// <returns>Native structured bytes.</returns>
    byte[] Serialize<T>(T value) where T : class, ISerializable;
    /// <summary>Restores one complete compiler value.</summary>
    /// <typeparam name="T">The expected value contract.</typeparam>
    /// <param name="bytes">Native structured bytes.</param>
    /// <returns>A detached value.</returns>
    T Deserialize<T>(ReadOnlySpan<byte> bytes) where T : class, ISerializable;
}

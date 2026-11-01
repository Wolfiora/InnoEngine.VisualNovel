using System;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Player.Ports;

/// <summary>Supplies generation-owned engine serialization for neutral VN state.</summary>
public interface INovelStateCodec
{
    /// <summary>Encodes a closed neutral contract through the engine's native serializer.</summary>
    /// <typeparam name="T">The declared state contract.</typeparam>
    /// <param name="value">The detached complete state.</param>
    /// <returns>Owned native bytes.</returns>
    byte[] Encode<T>(T value) where T : class, ISerializable;
    /// <summary>Decodes a closed neutral contract through the engine's native serializer.</summary>
    /// <typeparam name="T">The declared state contract.</typeparam>
    /// <param name="bytes">Complete native bytes.</param>
    /// <returns>A detached candidate; malformed data must throw.</returns>
    T Decode<T>(ReadOnlySpan<byte> bytes) where T : class, ISerializable;
}

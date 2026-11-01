using System;
using InnoEngine.Serialization;
using Inno.VisualNovel.Player.Ports;

namespace Inno.VisualNovel.Authoring.Preview;

/// <summary>Adapts the editor generation's native serializer for isolated preview save storage.</summary>
public sealed class EditorNovelStateCodec : INovelStateCodec
{
    private readonly SerializationRegistry m_serialization;
    /// <summary>Creates an adapter that never outlives its editor serialization generation.</summary>
    /// <param name="serialization">The host-owned active registry.</param>
    public EditorNovelStateCodec(SerializationRegistry serialization) => m_serialization = serialization ?? throw new ArgumentNullException(nameof(serialization));
    /// <inheritdoc />
    public byte[] Encode<T>(T value) where T : class, ISerializable => m_serialization.Serialize(value);
    /// <inheritdoc />
    public T Decode<T>(ReadOnlySpan<byte> bytes) where T : class, ISerializable => m_serialization.Deserialize<T>(bytes);
}

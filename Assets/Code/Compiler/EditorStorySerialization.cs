using System;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Compiler;

/// <summary>Adapts the editor serializer for proven reference-free story compiler data.</summary>
public sealed class EditorStorySerialization : IStorySerialization
{
    private readonly SerializationRegistry m_serialization;
    /// <summary>Creates a scoped adapter without owning the editor registry.</summary>
    /// <param name="serialization">The current editor serialization owner.</param>
    public EditorStorySerialization(SerializationRegistry serialization) => m_serialization = serialization ?? throw new ArgumentNullException(nameof(serialization));
    /// <inheritdoc />
    public byte[] Serialize<T>(T value) where T : class, ISerializable => m_serialization.Serialize(value);
    /// <inheritdoc />
    public T Deserialize<T>(ReadOnlySpan<byte> bytes) where T : class, ISerializable => m_serialization.Deserialize<T>(bytes);
}

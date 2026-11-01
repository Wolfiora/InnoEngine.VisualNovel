using System;
using InnoEngine.Serialization;
using InnoEditor.Assets;
using Inno.VisualNovel.Compiler;

namespace Inno.VisualNovel.Authoring.AssetImporting;

internal sealed class ImportStorySerialization(AssetSerializationServices services) : IStorySerialization
{
    public byte[] Serialize<T>(T value) where T : class, ISerializable => services.Serialize(value);
    public T Deserialize<T>(ReadOnlySpan<byte> bytes) where T : class, ISerializable => services.Deserialize<T>(bytes);
}

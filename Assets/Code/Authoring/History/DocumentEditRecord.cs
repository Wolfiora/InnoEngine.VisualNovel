using System;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Authoring.History;

[GenerateSerializationConverter]
internal sealed class DocumentEditRecord : ISerializable
{
    [SerializableProperty] public Guid documentId { get; set; }
    [SerializableProperty] public byte[] before { get; set; } = [];
    [SerializableProperty] public byte[] after { get; set; } = [];
}

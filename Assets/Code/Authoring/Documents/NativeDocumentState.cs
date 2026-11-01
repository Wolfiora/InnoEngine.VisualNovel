using System;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Authoring.Documents;

[GenerateSerializationConverter]
internal sealed class NativeDocumentState : ISerializable
{
    [SerializableProperty] public Guid documentId { get; set; }
    [SerializableProperty] public string path { get; set; } = string.Empty;
    [SerializableProperty] public byte[] current { get; set; } = [];
    [SerializableProperty] public byte[] saved { get; set; } = [];
    [SerializableProperty] public string sourceHash { get; set; } = string.Empty;
    internal long revision { get; set; }
}

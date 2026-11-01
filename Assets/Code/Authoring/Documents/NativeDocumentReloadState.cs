using System;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Authoring.Documents;

[GenerateSerializationConverter]
internal sealed class NativeDocumentReloadState : ISerializable
{
    [SerializableProperty] public Guid activeDocument { get; set; }
    [SerializableProperty] public NativeDocumentState[] documents { get; set; } = [];
}

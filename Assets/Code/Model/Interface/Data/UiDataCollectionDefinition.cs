using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Interface.Data;

/// <summary>Declares stable keyed row fields for an authored repeated UI container.</summary>
[GenerateSerializationConverter]
public sealed class UiDataCollectionDefinition : ISerializable
{
    /// <summary>Gets or sets the local collection identity.</summary>
    [SerializableProperty] public string id { get; set; } = string.Empty;
    /// <summary>Gets or sets its author-facing label.</summary>
    [SerializableProperty] public string title { get; set; } = string.Empty;
    /// <summary>Gets or sets the exact scalar field schema of each row.</summary>
    [SerializableProperty] public UiDataFieldDefinition[] fields { get; set; } = [];
}

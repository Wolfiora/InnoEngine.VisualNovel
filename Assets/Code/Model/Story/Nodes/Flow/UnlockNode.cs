using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Story.Nodes.Flow;

/// <summary>Requests a monotonic global collection unlock at an explicit story boundary.</summary>
[GenerateSerializationConverter]
public sealed class UnlockNode : ISerializable
{
    /// <summary>Gets or sets the declared collection identity.</summary>
    [SerializableProperty] public string entryId { get; set; } = string.Empty;
}

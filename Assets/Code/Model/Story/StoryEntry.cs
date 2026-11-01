using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Story;

/// <summary>Names an externally addressable node in a chapter.</summary>
[GenerateSerializationConverter]
public sealed class StoryEntry : ISerializable
{
    /// <summary>Gets or sets the stable public entry identity.</summary>
    [SerializableProperty]
    public string id { get; set; } = "start";
    /// <summary>Gets or sets the target node identity.</summary>
    [SerializableProperty]
    public string nodeId { get; set; } = string.Empty;
}

using InnoEngine.Serialization;

namespace Inno.VisualNovel.Player.Persistence;

/// <summary>Stores a detached line suitable for history and read tracking.</summary>
[GenerateSerializationConverter]
public sealed class ReadingRecord : ISerializable
{
    /// <summary>Gets or sets stable source location.</summary>
    [SerializableProperty] public string nodeId { get; set; } = string.Empty;
    /// <summary>Gets or sets the resolved speaker.</summary>
    [SerializableProperty] public string speaker { get; set; } = string.Empty;
    /// <summary>Gets or sets the original stable character used by independent voice preferences.</summary>
    [SerializableProperty] public string characterId { get; set; } = string.Empty;
    /// <summary>Gets or sets resolved complete text.</summary>
    [SerializableProperty] public string text { get; set; } = string.Empty;
    /// <summary>Gets or sets the line's content hash.</summary>
    [SerializableProperty] public string contentId { get; set; } = string.Empty;
    /// <summary>Gets or sets the original voice cue used for history playback.</summary>
    [SerializableProperty] public string voiceCueId { get; set; } = string.Empty;
}

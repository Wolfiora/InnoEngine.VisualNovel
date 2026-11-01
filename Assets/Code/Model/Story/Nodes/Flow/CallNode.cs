using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Story.Nodes.Flow;

/// <summary>Invokes a named entry while retaining a separate continuation.</summary>
[GenerateSerializationConverter]
public sealed class CallNode : ISerializable
{
    /// <summary>Gets or sets the named entry of the current program.</summary>
    [SerializableProperty]
    public string entryId { get; set; } = "start";
    /// <summary>Gets or sets the destination chapter, empty for the current chapter.</summary>
    [SerializableProperty] public string chapterId { get; set; } = string.Empty;
    /// <summary>Gets or sets whether this is a jump without adding a return continuation.</summary>
    [SerializableProperty] public bool jump { get; set; }
}

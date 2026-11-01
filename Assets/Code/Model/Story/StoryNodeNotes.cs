using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Story;

/// <summary>Stores author-only node annotations without contributing control-flow edges.</summary>
[GenerateSerializationConverter]
public sealed class StoryNodeNotes : ISerializable
{
    /// <summary>Gets or sets the multiline author note.</summary>
    [SerializableProperty] public string note { get; set; } = string.Empty;
    /// <summary>Gets or sets unique searchable author tags.</summary>
    [SerializableProperty] public string[] tags { get; set; } = [];
    /// <summary>Gets or sets whether this node appears in the chapter's bookmark filter.</summary>
    [SerializableProperty] public bool bookmarked { get; set; }
}

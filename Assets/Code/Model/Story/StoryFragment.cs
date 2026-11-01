using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Story;

/// <summary>Groups stable nodes without defining their execution order.</summary>
[GenerateSerializationConverter]
public sealed class StoryFragment : ISerializable
{
    /// <summary>Gets or sets the stable fragment identity.</summary>
    [SerializableProperty]
    public string id { get; set; } = string.Empty;
    /// <summary>Gets or sets the author-facing name.</summary>
    [SerializableProperty]
    public string name { get; set; } = string.Empty;
    /// <summary>Gets or sets the fragment members in author-facing display order.</summary>
    [SerializableProperty]
    public string[] nodes { get; set; } = [];
}

using InnoEngine.Serialization;

namespace Inno.VisualNovel.Narrative.Execution;

/// <summary>Identifies presentation work that must be released when an unfinished task is cancelled.</summary>
[GenerateSerializationConverter]
public sealed class StoryOwnedResource : ISerializable
{
    /// <summary>Gets or sets audio, performance or particles.</summary>
    [SerializableProperty] public string kind { get; set; } = string.Empty;
    /// <summary>Gets or sets the logical slot or instance identity.</summary>
    [SerializableProperty] public string id { get; set; } = string.Empty;
}

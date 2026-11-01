using InnoEngine.Serialization;

namespace Inno.VisualNovel.Narrative.Execution;

/// <summary>Captures one deterministic parallel branch and the presentation resources it owns.</summary>
[GenerateSerializationConverter]
public sealed class StoryTaskState : ISerializable
{
    /// <summary>Gets or sets the stable branch port identity.</summary>
    [SerializableProperty] public string branch { get; set; } = string.Empty;
    /// <summary>Gets or sets the explicit child interpreter continuation.</summary>
    [SerializableProperty] public StoryExecutionState execution { get; set; } = new();
}

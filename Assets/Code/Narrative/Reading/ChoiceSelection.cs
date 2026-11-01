using InnoEngine.Serialization;

namespace Inno.VisualNovel.Narrative.Reading;

/// <summary>Captures a selected option in the current game and rollback branch.</summary>
[GenerateSerializationConverter]
public sealed class ChoiceSelection : ISerializable
{
    /// <summary>Gets or sets the linked stable choice instruction.</summary>
    [SerializableProperty] public string nodeId { get; set; } = string.Empty;
    /// <summary>Gets or sets the selected authored option.</summary>
    [SerializableProperty] public string optionId { get; set; } = string.Empty;
}

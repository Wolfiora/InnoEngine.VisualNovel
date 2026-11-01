using InnoEngine.Serialization;

namespace Inno.VisualNovel.Narrative.Reading;

/// <summary>Freezes one textual substitution at line entry for safe localization and snapshot restoration.</summary>
[GenerateSerializationConverter]
public sealed class DialogueParameter : ISerializable
{
    /// <summary>Gets or sets the declared variable identity.</summary>
    [SerializableProperty] public string id { get; set; } = string.Empty;
    /// <summary>Gets or sets the literal resolved value independently of later variable changes.</summary>
    [SerializableProperty] public string value { get; set; } = string.Empty;
}

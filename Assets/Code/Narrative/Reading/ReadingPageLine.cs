using InnoEngine.Serialization;

namespace Inno.VisualNovel.Narrative.Reading;

/// <summary>Captures one page-local NVL line without retaining mutable game state or source assets.</summary>
[GenerateSerializationConverter]
public sealed class ReadingPageLine : ISerializable
{
    /// <summary>Gets or sets the linked instruction identity used to resolve translations.</summary>
    [SerializableProperty] public string nodeId { get; set; } = string.Empty;
    /// <summary>Gets or sets resolved display markup with literal interpolation values escaped.</summary>
    [SerializableProperty] public string markup { get; set; } = string.Empty;
    /// <summary>Gets or sets the resolved speaker.</summary>
    [SerializableProperty] public string speaker { get; set; } = string.Empty;
    /// <summary>Gets or sets the language of this resolved line.</summary>
    [SerializableProperty] public string locale { get; set; } = string.Empty;
    /// <summary>Gets or sets frozen textual substitutions for later language reflow.</summary>
    [SerializableProperty] public DialogueParameter[] parameters { get; set; } = [];
}

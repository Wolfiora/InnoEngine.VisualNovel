using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Story.Nodes.Reading;

/// <summary>Defines one stable line of dialogue independently of graph topology.</summary>
[GenerateSerializationConverter]
public sealed class DialogueNode : ISerializable
{
    /// <summary>Gets or sets the stable text identity used by reading and localization.</summary>
    [SerializableProperty]
    public string textId { get; set; } = string.Empty;
    /// <summary>Gets or sets the stable character resource identity, empty for unlinked dialogue.</summary>
    [SerializableProperty]
    public string characterId { get; set; } = string.Empty;
    /// <summary>Gets or sets the optional interpolated speaker override; otherwise a linked character supplies its name.</summary>
    [SerializableProperty]
    public string speaker { get; set; } = string.Empty;
    /// <summary>Gets or sets the source text with typed variable interpolation.</summary>
    [SerializableProperty]
    public string text { get; set; } = string.Empty;
    /// <summary>Gets or sets the declared non-looping voice cue, empty for an unvoiced line.</summary>
    [SerializableProperty]
    public string voiceCueId { get; set; } = string.Empty;
    /// <summary>Gets or sets the reveal rate in grapheme clusters per second.</summary>
    [SerializableProperty]
    public double charactersPerSecond { get; set; } = 30;
    /// <summary>Gets or sets a logical pause after sentence punctuation.</summary>
    [SerializableProperty]
    public double punctuationPause { get; set; }
    /// <summary>Gets or sets the reading presentation selected by the author.</summary>
    [SerializableProperty] public DialoguePresentation presentation { get; set; }
    /// <summary>Gets or sets whether this NVL line begins a fresh page.</summary>
    [SerializableProperty] public bool clearPage { get; set; }
    /// <summary>Gets or sets the normalized left position for positioned reading layouts.</summary>
    [SerializableProperty] public double anchorX { get; set; } = 0.25;
    /// <summary>Gets or sets the normalized top position for positioned reading layouts.</summary>
    [SerializableProperty] public double anchorY { get; set; } = 0.25;
    /// <summary>Gets or sets the normalized text-panel width for positioned layouts.</summary>
    [SerializableProperty] public double relativeWidth { get; set; } = 0.5;
}

using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Localization;

/// <summary>Stores translated text and optional voice against an exact source revision.</summary>
[GenerateSerializationConverter]
public sealed class LocalizedTranslation : ISerializable
{
    /// <summary>Gets or sets the declared language tag.</summary>
    [SerializableProperty] public string language { get; set; } = string.Empty;
    /// <summary>Gets or sets translated text, including intentionally empty translations.</summary>
    [SerializableProperty] public string text { get; set; } = string.Empty;
    /// <summary>Gets or sets the source-text digest approved by the translator.</summary>
    [SerializableProperty] public string sourceFingerprint { get; set; } = string.Empty;
    /// <summary>Gets or sets the review state.</summary>
    [SerializableProperty] public TranslationStatus status { get; set; }
    /// <summary>Gets or sets a localized voice cue; empty retains the authored voice.</summary>
    [SerializableProperty] public string voiceCueId { get; set; } = string.Empty;
}

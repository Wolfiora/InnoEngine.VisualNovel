using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Localization;

/// <summary>Associates one stable content identity with its source and translations.</summary>
[GenerateSerializationConverter]
public sealed class LocalizedText : ISerializable
{
    /// <summary>Gets or sets the stable text identity.</summary>
    [SerializableProperty] public string id { get; set; } = string.Empty;
    /// <summary>Gets or sets the current authored source.</summary>
    [SerializableProperty] public string source { get; set; } = string.Empty;
    /// <summary>Gets or sets retained translation work in declared languages.</summary>
    [SerializableProperty] public LocalizedTranslation[] translations { get; set; } = [];
}

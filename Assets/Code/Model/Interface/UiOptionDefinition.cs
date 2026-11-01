using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Model.Interface;

/// <summary>Declares a stable, localizable choice for a typed UI selector.</summary>
[GenerateSerializationConverter]
public sealed class UiOptionDefinition : ISerializable
{
    /// <summary>Gets or sets the option's stable control-local identity.</summary>
    [SerializableProperty] public string id { get; set; } = string.Empty;
    /// <summary>Gets or sets the source label.</summary>
    [SerializableProperty] public string text { get; set; } = string.Empty;
    /// <summary>Gets or sets the stable localization identity.</summary>
    [SerializableProperty] public string textId { get; set; } = string.Empty;
    /// <summary>Gets or sets the typed value written only after confirmation.</summary>
    [SerializableProperty] public NovelValue value { get; set; } = NovelValue.String(string.Empty);
    /// <summary>Gets or sets whether the option can be selected.</summary>
    [SerializableProperty] public bool enabled { get; set; } = true;
}

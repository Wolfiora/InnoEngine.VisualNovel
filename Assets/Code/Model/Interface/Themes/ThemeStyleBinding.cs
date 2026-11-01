using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Interface.Themes;

/// <summary>Assigns one named color or metric to a single explicit style field.</summary>
[GenerateSerializationConverter]
public sealed class ThemeStyleBinding : ISerializable
{
    /// <summary>Gets or sets exactly one supported token-bindable field.</summary>
    [SerializableProperty] public UiStyleProperty property { get; set; }
    /// <summary>Gets or sets a color or metric token key of the corresponding type.</summary>
    [SerializableProperty] public string token { get; set; } = string.Empty;
}

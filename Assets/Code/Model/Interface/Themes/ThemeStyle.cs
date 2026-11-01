using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Interface.Themes;

/// <summary>Defines a named inheritable style with explicit field ownership.</summary>
[GenerateSerializationConverter]
public sealed class ThemeStyle : ISerializable
{
    /// <summary>Gets or sets the stable theme-local style class.</summary>
    [SerializableProperty] public string key { get; set; } = string.Empty;
    /// <summary>Gets or sets the optional parent style key.</summary>
    [SerializableProperty] public string parent { get; set; } = string.Empty;
    /// <summary>Gets or sets fields contributed after the inherited parent.</summary>
    [SerializableProperty] public UiStyleProperty fields { get; set; }
    /// <summary>Gets or sets literal values for owned fields.</summary>
    [SerializableProperty] public UiStyleDefinition values { get; set; } = new();
    /// <summary>Gets or sets color and metric token substitutions within owned fields.</summary>
    [SerializableProperty] public ThemeStyleBinding[] bindings { get; set; } = [];
}

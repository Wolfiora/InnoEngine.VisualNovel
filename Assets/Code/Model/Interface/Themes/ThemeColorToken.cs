using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Interface.Themes;

/// <summary>Names one reusable packed RRGGBBAA theme color.</summary>
[GenerateSerializationConverter]
public sealed class ThemeColorToken : ISerializable
{
    /// <summary>Gets or sets the stable theme-local key.</summary>
    [SerializableProperty] public string key { get; set; } = string.Empty;
    /// <summary>Gets or sets the packed sRGB color.</summary>
    [SerializableProperty] public uint value { get; set; } = 0xFFFFFFFF;
}

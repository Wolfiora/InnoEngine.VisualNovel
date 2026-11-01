using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Interface.Themes;

/// <summary>Names one reusable finite logical dimension or multiplier.</summary>
[GenerateSerializationConverter]
public sealed class ThemeMetricToken : ISerializable
{
    /// <summary>Gets or sets the stable theme-local key.</summary>
    [SerializableProperty] public string key { get; set; } = string.Empty;
    /// <summary>Gets or sets the logical metric value.</summary>
    [SerializableProperty] public float value { get; set; }
}

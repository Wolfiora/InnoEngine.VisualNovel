using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Interface;

/// <summary>Declares optional numeric bindings for a control's design-space rectangle.</summary>
[GenerateSerializationConverter]
public sealed class UiGeometryBinding : ISerializable
{
    /// <summary>Gets or sets the horizontal-position field, empty for authored geometry.</summary>
    [SerializableProperty] public string x { get; set; } = string.Empty;
    /// <summary>Gets or sets the vertical-position field, empty for authored geometry.</summary>
    [SerializableProperty] public string y { get; set; } = string.Empty;
    /// <summary>Gets or sets the width field, empty for authored geometry.</summary>
    [SerializableProperty] public string width { get; set; } = string.Empty;
    /// <summary>Gets or sets the height field, empty for authored geometry.</summary>
    [SerializableProperty] public string height { get; set; } = string.Empty;
    /// <summary>Gets or sets whether bound values are fractions of the authored page canvas instead of pixels.</summary>
    [SerializableProperty] public bool normalized { get; set; }
}

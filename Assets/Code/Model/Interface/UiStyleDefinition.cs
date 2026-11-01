using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Interface;

/// <summary>Defines editable packed sRGB control colors, alpha and logical text dimensions.</summary>
[GenerateSerializationConverter]
public sealed class UiStyleDefinition : ISerializable
{
    /// <summary>Gets or sets the normal fill, packed as RRGGBBAA.</summary>
    [SerializableProperty] public uint background { get; set; } = 0x202534FF;
    /// <summary>Gets or sets hovered fill.</summary>
    [SerializableProperty] public uint hover { get; set; } = 0x343E55FF;
    /// <summary>Gets or sets pressed fill.</summary>
    [SerializableProperty] public uint pressed { get; set; } = 0x44597AFF;
    /// <summary>Gets or sets disabled fill.</summary>
    [SerializableProperty] public uint disabled { get; set; } = 0x242630FF;
    /// <summary>Gets or sets text color.</summary>
    [SerializableProperty] public uint foreground { get; set; } = 0xF4F5F8FF;
    /// <summary>Gets or sets disabled text color independently of the disabled control fill.</summary>
    [SerializableProperty] public uint disabledForeground { get; set; } = 0xA6ACB8FF;
    /// <summary>Gets or sets image tint independently of the control's text color.</summary>
    [SerializableProperty] public uint imageTint { get; set; } = 0xFFFFFFFF;
    /// <summary>Gets or sets focus outline color.</summary>
    [SerializableProperty] public uint focus { get; set; } = 0x93BFFFFF;
    /// <summary>Gets or sets font size in design pixels.</summary>
    [SerializableProperty] public float fontSize { get; set; } = 28;
    /// <summary>Gets or sets space between text lines.</summary>
    [SerializableProperty] public float lineSpacing { get; set; } = 1.35f;
    /// <summary>Gets or sets text inset.</summary>
    [SerializableProperty] public float textPadding { get; set; } = 16;
    /// <summary>Gets or sets opacity applied to the complete element.</summary>
    [SerializableProperty] public float opacity { get; set; } = 1;
    /// <summary>Gets or sets alignment within each wrapped text line.</summary>
    [SerializableProperty] public UiTextAlignment alignment { get; set; }
    /// <summary>Copies all literal style values independently of a page or theme declaration.</summary>
    /// <returns>An editable value copy.</returns>
    public UiStyleDefinition Copy() => new()
    {
        background = background, hover = hover, pressed = pressed, disabled = disabled, foreground = foreground, disabledForeground = disabledForeground,
        imageTint = imageTint, focus = focus, fontSize = fontSize, lineSpacing = lineSpacing, textPadding = textPadding, opacity = opacity, alignment = alignment
    };
}

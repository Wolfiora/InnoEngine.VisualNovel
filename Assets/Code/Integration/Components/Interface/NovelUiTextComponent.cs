using InnoEngine.Reflection;
using InnoEngine.Scene;
using InnoEngine.Serialization;
using InnoEngine.Text;
using Inno.VisualNovel.Model.Interface;

namespace Inno.VisualNovel.Integration.Components.Interface;

/// <summary>Owns editable text and typography on a real object; shaping remains an engine Text responsibility.</summary>
[StableTypeId("b0f32c52-b546-4259-9e9e-d4cd7ebc9c25")]
public sealed class NovelUiTextComponent : GameComponent
{
    /// <summary>Gets or sets literal authored text, including any game language.</summary>
    [SerializableProperty] public string text { get; set; } = "Text";
    /// <summary>Gets or sets a stable game-localization key; an empty key uses literal text.</summary>
    [SerializableProperty] public string textId { get; set; } = string.Empty;
    /// <summary>Gets or sets whether text uses the existing VN rich-text grammar.</summary>
    [SerializableProperty] public bool richText { get; set; }
    /// <summary>Gets or sets the imported font; null inherits the UI root's font.</summary>
    [SerializableProperty] public FontAsset? font { get; set; }
    /// <summary>Gets or sets logical type size.</summary>
    [SerializableProperty] public float fontSize { get; set; } = 28;
    /// <summary>Gets or sets the shaped line-height multiplier.</summary>
    [SerializableProperty] public float lineSpacing { get; set; } = 1.35f;
    /// <summary>Gets or sets text inset.</summary>
    [SerializableProperty] public float padding { get; set; } = 8;
    /// <summary>Gets or sets wrapped-line alignment.</summary>
    [SerializableProperty] public UiTextAlignment alignment { get; set; }
    /// <summary>Gets or sets packed RRGGBBAA text color.</summary>
    [SerializableProperty] public uint color { get; set; } = 0xFFFFFFFF;
}

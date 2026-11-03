using InnoEngine.Assets;
using InnoEngine.Reflection;
using InnoEngine.Serialization;
using InnoEngine.Text;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Model.Interface;

/// <summary>Stores one editable, backend-independent game screen.</summary>
[StableTypeId("fd0c7316-14a5-4d89-8492-bfd0a0b9f110")]
public sealed class ScreenAsset : AssetObject
{
    /// <summary>Gets or sets the author-facing title.</summary>
    [SerializableProperty] public string title { get; set; } = "New screen";
    /// <summary>Gets or sets the logical canvas width.</summary>
    [SerializableProperty] public float width { get; set; } = 1920;
    /// <summary>Gets or sets the logical canvas height.</summary>
    [SerializableProperty] public float height { get; set; } = 1080;
    /// <summary>Gets or sets the page default font.</summary>
    [SerializableProperty] public FontAsset? font { get; set; }
    /// <summary>Gets or sets page-owned alternate font bindings for rich text.</summary>
    [SerializableProperty] public UiTextFont[] textFonts { get; set; } = [];
    /// <summary>Gets or sets page-owned inline image bindings for rich text.</summary>
    [SerializableProperty] public UiTextIcon[] textIcons { get; set; } = [];
    /// <summary>Gets or sets whether the screen blocks lower page input.</summary>
    [SerializableProperty] public bool modal { get; set; }
    /// <summary>Gets or sets an authored static control identity preferred when the page first receives focus.</summary>
    [SerializableProperty] public string initialFocus { get; set; } = string.Empty;
    /// <summary>Gets or sets page-owned typed fields; Local resets on navigation and Game participates in saves and rollback.</summary>
    [SerializableProperty] public VariableDefinition[] stateVariables { get; set; } = [];
    /// <summary>Gets or sets the element tree.</summary>
    [SerializableProperty] public UiElementDefinition root { get; set; } = new()
    {
        id = "root", kind = UiElementKind.Panel,
        layout = new() { widthMode = UiSizeMode.Fill, heightMode = UiSizeMode.Fill },
        style = new() { background = 0x00000000 }
    };
}

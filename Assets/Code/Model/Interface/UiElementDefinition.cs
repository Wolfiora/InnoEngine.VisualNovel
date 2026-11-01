using InnoEngine.Rendering;
using InnoEngine.Text;
using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Interface.Themes;

namespace Inno.VisualNovel.Model.Interface;

/// <summary>Defines one authored element in an editable VN screen.</summary>
[RequiresSerializationConverter]
public sealed class UiElementDefinition : ISerializable
{
    /// <summary>Gets or sets a theme style class, empty to use only the page's literal values.</summary>
    [SerializableProperty] public string styleClass { get; set; } = string.Empty;
    /// <summary>Gets or sets explicit local fields overriding the named theme class.</summary>
    [SerializableProperty] public UiStyleProperty styleOverrides { get; set; }
    /// <summary>Gets or sets the stable page-local identity.</summary>
    [SerializableProperty] public string id { get; set; } = string.Empty;
    /// <summary>Gets or sets the implemented control kind.</summary>
    [SerializableProperty] public UiElementKind kind { get; set; }
    /// <summary>Gets or sets the authored layout.</summary>
    [SerializableProperty] public UiLayoutDefinition layout { get; set; } = new();
    /// <summary>Gets or sets optional numeric rectangle bindings independent of authored fallback geometry.</summary>
    [SerializableProperty] public UiGeometryBinding geometryBinding { get; set; } = new();
    /// <summary>Gets or sets pure visibility, interaction and waiting presentation animations.</summary>
    [SerializableProperty] public Animation.UiAnimationDefinition animation { get; set; } = new();
    /// <summary>Gets or sets the authored style.</summary>
    [SerializableProperty] public UiStyleDefinition style { get; set; } = new();
    /// <summary>Gets or sets child definitions in paint and navigation order.</summary>
    [SerializableProperty] public UiElementDefinition[] children { get; set; } = [];
    /// <summary>Gets or sets literal fallback text.</summary>
    [SerializableProperty] public string text { get; set; } = string.Empty;
    /// <summary>Gets or sets a stable localization identity for literal text.</summary>
    [SerializableProperty] public string textId { get; set; } = string.Empty;
    /// <summary>Gets or sets plain fallback help shown on pointer dwell or keyboard focus.</summary>
    [SerializableProperty] public string tooltip { get; set; } = string.Empty;
    /// <summary>Gets or sets the independent stable localization identity of the help text.</summary>
    [SerializableProperty] public string tooltipTextId { get; set; } = string.Empty;
    /// <summary>Gets or sets a String field supplying help, including a repeated row's disabled reason.</summary>
    [SerializableProperty] public string tooltipBinding { get; set; } = string.Empty;
    /// <summary>Gets or sets whether literal or ordinary bound text interprets authored formatting tags.</summary>
    [SerializableProperty] public bool richText { get; set; }
    /// <summary>Gets or sets the typed data field supplying text or control values.</summary>
    [SerializableProperty] public string binding { get; set; } = string.Empty;
    /// <summary>Gets or sets the collection field repeated using this container's child templates.</summary>
    [SerializableProperty] public string repeatBinding { get; set; } = string.Empty;
    /// <summary>Gets or sets a Boolean field enabling tail following in a growing scroll document.</summary>
    [SerializableProperty] public string followEndBinding { get; set; } = string.Empty;
    /// <summary>Gets or sets the Boolean visibility field, empty for always visible.</summary>
    [SerializableProperty] public string visibleBinding { get; set; } = string.Empty;
    /// <summary>Gets or sets the Boolean interactivity field, empty for enabled.</summary>
    [SerializableProperty] public string enabledBinding { get; set; } = string.Empty;
    /// <summary>Gets or sets a stable registered action identity.</summary>
    [SerializableProperty] public string action { get; set; } = string.Empty;
    /// <summary>Gets or sets the action's neutral string argument.</summary>
    [SerializableProperty] public string argument { get; set; } = string.Empty;
    /// <summary>Gets or sets the typed field supplying an action argument.</summary>
    [SerializableProperty] public string argumentBinding { get; set; } = string.Empty;
    /// <summary>Gets or sets the imported font, or null to use the page default.</summary>
    [SerializableProperty] public FontAsset? font { get; set; }
    /// <summary>Gets or sets the imported image.</summary>
    [SerializableProperty] public TextureAsset? image { get; set; }
    /// <summary>Gets or sets a typed image binding resolved by the active data source.</summary>
    [SerializableProperty] public string imageBinding { get; set; } = string.Empty;
    /// <summary>Gets or sets aspect-preserving image sizing.</summary>
    [SerializableProperty] public UiImageFit imageFit { get; set; }
    /// <summary>Gets or sets source-pixel borders for nine-slice image composition.</summary>
    [SerializableProperty] public UiImageSlice imageSlice { get; set; } = new();
    /// <summary>Gets or sets static selector options with stable independent identities.</summary>
    [SerializableProperty] public UiOptionDefinition[] options { get; set; } = [];
    /// <summary>Gets or sets a dynamic selector collection with text, value and optional enabled row fields.</summary>
    [SerializableProperty] public string optionsBinding { get; set; } = string.Empty;
    /// <summary>Gets or sets the slider lower limit.</summary>
    [SerializableProperty] public double minimum { get; set; }
    /// <summary>Gets or sets the slider upper limit.</summary>
    [SerializableProperty] public double maximum { get; set; } = 1;
    /// <summary>Gets or sets slider value quantization; zero uses continuous pointer input and an automatic keyboard step.</summary>
    [SerializableProperty] public double step { get; set; }
    /// <summary>Gets or sets the input limit in Unicode text elements.</summary>
    [SerializableProperty] public int maxLength { get; set; } = 64;
    /// <summary>Gets or sets whether this element may receive input.</summary>
    [SerializableProperty] public bool interactable { get; set; } = true;
    /// <summary>Gets or sets whether this painted element consumes pointer input.</summary>
    [SerializableProperty] public bool blocksPointer { get; set; } = true;
    /// <summary>Gets or sets whether this decorative subtree is painted below the stage and excluded from UI input.</summary>
    [SerializableProperty] public bool behindStage { get; set; }
}

using InnoEngine.Reflection;
using InnoEngine.Rendering;
using InnoEngine.Scene;
using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Interface;

namespace Inno.VisualNovel.Integration.Components.Interface;

/// <summary>Declares a native UI image; the selected SDK still requires an imported-texture bridge before this component can be projected.</summary>
[StableTypeId("b0f32c52-b546-4259-9e9e-d4cd7ebc9c24")]
public sealed class NovelUiImageComponent : GameComponent
{
    /// <summary>Gets or sets the imported image.</summary>
    [SerializableProperty] public TextureAsset? image { get; set; }
    /// <summary>Gets or sets image sizing within the resolved control rectangle.</summary>
    [SerializableProperty] public UiImageFit fit { get; set; }
    /// <summary>Gets or sets editable nine-slice source borders.</summary>
    [SerializableProperty] public UiImageSlice slice { get; set; } = new();
    /// <summary>Gets or sets the packed RRGGBBAA image tint.</summary>
    [SerializableProperty] public uint color { get; set; } = 0xFFFFFFFF;
}

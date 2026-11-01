using InnoEngine.Serialization;
using InnoEngine.Rendering;

namespace Inno.VisualNovel.Model.Interface;

/// <summary>Associates an inline text image key with an imported texture owned by the page.</summary>
[RequiresSerializationConverter]
public sealed class UiTextIcon : ISerializable
{
    /// <summary>Gets or sets the stable key used by inline image tags.</summary>
    [SerializableProperty] public string key { get; set; } = string.Empty;
    /// <summary>Gets or sets the imported image; missing references remain explicit.</summary>
    [SerializableProperty] public TextureAsset? image { get; set; }
}

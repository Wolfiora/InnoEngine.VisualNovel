using InnoEngine.Serialization;
using InnoEngine.Text;

namespace Inno.VisualNovel.Model.Interface;

/// <summary>Associates an authoring font key with an imported font owned by the page.</summary>
[RequiresSerializationConverter]
public sealed class UiTextFont : ISerializable
{
    /// <summary>Gets or sets the stable key used by font scopes.</summary>
    [SerializableProperty] public string key { get; set; } = string.Empty;
    /// <summary>Gets or sets the imported face; missing references remain explicit.</summary>
    [SerializableProperty] public FontAsset? font { get; set; }
}

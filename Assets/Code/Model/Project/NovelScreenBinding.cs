using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Interface;

namespace Inno.VisualNovel.Model.Project;

/// <summary>Associates one editable page with a stable shell role.</summary>
[RequiresSerializationConverter]
public sealed class NovelScreenBinding : ISerializable
{
    /// <summary>Gets or sets the role, such as title, reading, choice or pause.</summary>
    [SerializableProperty] public string role { get; set; } = string.Empty;
    /// <summary>Gets or sets the imported editable screen.</summary>
    [SerializableProperty] public ScreenAsset? screen { get; set; }
}

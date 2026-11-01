using InnoEngine.Assets;
using InnoEngine.Reflection;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Collections;

/// <summary>Stores the game's editable galleries, replays, endings and reference articles.</summary>
[StableTypeId("fd0c7316-14a5-4d89-8492-bfd0a0b9f181")]
public sealed class CollectionAsset : AssetObject
{
    /// <summary>Gets or sets the library title.</summary>
    [SerializableProperty] public string title { get; set; } = "收藏与资料";
    /// <summary>Gets or sets uniquely identified collection entries.</summary>
    [SerializableProperty] public CollectionEntry[] entries { get; set; } = [];
}

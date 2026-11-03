using InnoEngine.Assets;
using InnoEngine.Reflection;
using InnoEngine.Rendering;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Stages;

/// <summary>Stores independently editable scenery and its explicit image dependencies.</summary>
[StableTypeId("fd0c7316-14a5-4d89-8492-bfd0a0b9f140")]
public sealed class StageAsset : AssetObject
{
    /// <summary>Gets or sets the stable resource key used by story commands.</summary>
    [SerializableProperty] public string resourceId { get; set; } = "stage";
    /// <summary>Gets or sets the display name.</summary>
    [SerializableProperty] public string title { get; set; } = "New stage";
    /// <summary>Gets or sets design width.</summary>
    [SerializableProperty] public double width { get; set; } = 1920;
    /// <summary>Gets or sets design height.</summary>
    [SerializableProperty] public double height { get; set; } = 1080;
    /// <summary>Gets or sets native texture references retained by this stage.</summary>
    [SerializableProperty] public TextureAsset[] textures { get; set; } = [];
    /// <summary>Gets or sets authored layers.</summary>
    [SerializableProperty] public StageLayer[] layers { get; set; } = [];
}

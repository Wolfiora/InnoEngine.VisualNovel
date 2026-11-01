using InnoEngine.Assets;
using InnoEngine.Reflection;
using InnoEngine.Rendering;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Characters;

/// <summary>Stores layered character poses, expressions and explicit texture dependencies.</summary>
[StableTypeId("fd0c7316-14a5-4d89-8492-bfd0a0b9f141")]
public sealed class CharacterAsset : AssetObject
{
    /// <summary>Gets or sets the stable resource identity.</summary>
    [SerializableProperty] public string resourceId { get; set; } = "character";
    /// <summary>Gets or sets the displayed character name.</summary>
    [SerializableProperty] public string displayName { get; set; } = "角色";
    /// <summary>Gets or sets searchable alternative names without changing the stable identity.</summary>
    [SerializableProperty] public string[] aliases { get; set; } = [];
    /// <summary>Gets or sets the dialogue name tint in packed RGBA order.</summary>
    [SerializableProperty] public uint nameColor { get; set; } = 0xFFFFFFFF;
    /// <summary>Gets or sets the optional dialogue portrait independently of stage poses.</summary>
    [SerializableProperty] public TextureAsset? portrait { get; set; }
    /// <summary>Gets or sets the default pose identity.</summary>
    [SerializableProperty] public string defaultPose { get; set; } = "default";
    /// <summary>Gets or sets the default expression identity.</summary>
    [SerializableProperty] public string defaultExpression { get; set; } = "neutral";
    /// <summary>Gets or sets imported image references.</summary>
    [SerializableProperty] public TextureAsset[] textures { get; set; } = [];
    /// <summary>Gets or sets available poses.</summary>
    [SerializableProperty] public CharacterPose[] poses { get; set; } = [];
    /// <summary>Gets or sets available expressions.</summary>
    [SerializableProperty] public CharacterExpression[] expressions { get; set; } = [];
    /// <summary>Gets or sets optional deterministic breathing shared by all parts.</summary>
    [SerializableProperty] public CharacterBreathing breathing { get; set; } = new();
}

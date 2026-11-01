using InnoEngine.Assets;
using InnoEngine.Reflection;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Performance;

/// <summary>Provides native resource identity and author-facing metadata for VN visual effects.</summary>
[StableTypeId("fd0c7316-14a5-4d89-8492-bfd0a0b9f160")]
public sealed class VisualEffectAsset : AssetObject
{
    /// <summary>Gets or sets the explicit game-library key.</summary>
    [SerializableProperty] public string resourceId { get; set; } = string.Empty;
    /// <summary>Gets or sets the displayed preset name.</summary>
    [SerializableProperty] public string displayName { get; set; } = string.Empty;
    /// <summary>Gets or sets explanatory resource-library help.</summary>
    [SerializableProperty] public string description { get; set; } = string.Empty;
    /// <summary>Gets or sets the concrete payload used by this preset.</summary>
    [SerializableProperty] public VisualEffectKind kind { get; set; }
    /// <summary>Gets or sets the implemented particle definition carried by this native effect source.</summary>
    [SerializableProperty] public ParticlePreset particles { get; set; } = new();
    /// <summary>Gets or sets the image-operation payload for screen-effect presets.</summary>
    [SerializableProperty] public ScreenEffectDefinition screen { get; set; } = new();
    /// <summary>Gets or sets the image-composition payload for transition presets.</summary>
    [SerializableProperty] public TransitionDefinition transition { get; set; } = new();
}

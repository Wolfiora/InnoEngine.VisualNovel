using InnoEngine.Assets;
using InnoEngine.Reflection;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Performance;

/// <summary>Stores a reusable scalar stage performance without runtime callbacks.</summary>
[StableTypeId("fd0c7316-14a5-4d89-8492-bfd0a0b9f101")]
public sealed class PerformanceClipAsset : AssetObject
{
    /// <summary>Gets or sets the explicit reusable performance resource key.</summary>
    [SerializableProperty] public string resourceId { get; set; } = "performance";
    /// <summary>Gets or sets the author-facing clip name.</summary>
    [SerializableProperty]
    public string displayName { get; set; } = "New performance";
    /// <summary>Gets or sets the positive duration in seconds.</summary>
    [SerializableProperty]
    public double duration { get; set; } = 1;
    /// <summary>Gets or sets whether time wraps at the clip duration.</summary>
    [SerializableProperty]
    public bool loop { get; set; }
    /// <summary>Gets or sets whether reduced-motion mode preserves this story-essential performance.</summary>
    [SerializableProperty] public bool essential { get; set; }
    /// <summary>Gets or sets tracks with unique target/property pairs.</summary>
    [SerializableProperty]
    public PerformanceTrack[] tracks { get; set; } = [];
    /// <summary>Gets or sets bounded named synchronization points with unique clip-local identities.</summary>
    [SerializableProperty] public PerformanceMarker[] markers { get; set; } = [];
}

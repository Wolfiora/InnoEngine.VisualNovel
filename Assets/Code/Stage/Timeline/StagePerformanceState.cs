using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Performance;

namespace Inno.VisualNovel.Stage.Timeline;

/// <summary>Stores one active or completed performance's restorable property ownership.</summary>
[GenerateSerializationConverter]
public sealed class StagePerformanceState : ISerializable
{
    /// <summary>Gets or sets the stable active instance identity.</summary>
    [SerializableProperty] public string instanceId { get; set; } = string.Empty;
    /// <summary>Gets or sets the imported clip resource key.</summary>
    [SerializableProperty] public string clipId { get; set; } = string.Empty;
    /// <summary>Gets or sets resolved target bindings.</summary>
    [SerializableProperty] public PerformanceBinding[] bindings { get; set; } = [];
    /// <summary>Gets or sets pure logical playback position and pause state.</summary>
    [SerializableProperty] public PerformanceState playback { get; set; } = new();
}

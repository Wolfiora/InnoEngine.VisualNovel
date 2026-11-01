using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Performance;

namespace Inno.VisualNovel.Model.Story.Nodes.Stage;

/// <summary>Controls a reusable, explicitly bound stage performance.</summary>
[GenerateSerializationConverter]
public sealed class PerformanceNode : ISerializable
{
    /// <summary>Gets or sets the requested lifecycle action.</summary>
    [SerializableProperty] public PerformanceOperation operation { get; set; } = PerformanceOperation.Cancel;
    /// <summary>Gets or sets the stable active performance identity.</summary>
    [SerializableProperty] public string instanceId { get; set; } = "performance";
    /// <summary>Gets or sets the explicit clip resource key required for Play.</summary>
    [SerializableProperty] public string clipId { get; set; } = string.Empty;
    /// <summary>Gets or sets reusable slot-to-owner bindings.</summary>
    [SerializableProperty] public PerformanceBinding[] bindings { get; set; } = [];
    /// <summary>Gets or sets nonnegative absolute logical seconds for Seek.</summary>
    [SerializableProperty] public double time { get; set; }
    /// <summary>Gets or sets the clip-local synchronization identity required by WaitMarker.</summary>
    [SerializableProperty] public string markerId { get; set; } = string.Empty;
}

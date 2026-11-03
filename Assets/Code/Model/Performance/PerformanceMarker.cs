using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Performance;

/// <summary>Names a pure logical synchronization point without introducing timeline side effects.</summary>
[GenerateSerializationConverter]
public sealed class PerformanceMarker : ISerializable
{
    /// <summary>Gets or sets the stable clip-local marker identity used by narrative waits.</summary>
    [SerializableProperty] public string id { get; set; } = "marker";
    /// <summary>Gets or sets the author-facing label.</summary>
    [SerializableProperty] public string name { get; set; } = "Marker";
    /// <summary>Gets or sets the logical time between zero and the clip duration.</summary>
    [SerializableProperty] public double time { get; set; }
    /// <summary>Creates a detached marker retaining its stable identity.</summary>
    /// <returns>An independently editable marker.</returns>
    public PerformanceMarker Copy() => new() { id = id, name = name, time = time };
}

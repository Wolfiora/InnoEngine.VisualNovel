using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Performance;

/// <summary>Targets one named visual object's scalar property.</summary>
[GenerateSerializationConverter]
public sealed class PerformanceTrack : ISerializable
{
    /// <summary>Gets or sets the target slot, resolved by the stage owner.</summary>
    [SerializableProperty]
    public string target { get; set; } = string.Empty;
    /// <summary>Gets or sets the animated scalar channel.</summary>
    [SerializableProperty]
    public PerformanceProperty property { get; set; }
    /// <summary>Gets or sets strictly time-ordered keyframes.</summary>
    [SerializableProperty]
    public PerformanceKeyframe[] keys { get; set; } = [];
}

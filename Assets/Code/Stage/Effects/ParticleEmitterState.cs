using InnoEngine.Serialization;

namespace Inno.VisualNovel.Stage.Effects;

/// <summary>Stores all neutral state required to reconstruct an emitter at any logical time.</summary>
[GenerateSerializationConverter]
public sealed class ParticleEmitterState : ISerializable
{
    /// <summary>Gets or sets the stage-local emitter identity.</summary>
    [SerializableProperty] public string id { get; set; } = string.Empty;
    /// <summary>Gets or sets its declared preset key.</summary>
    [SerializableProperty] public string presetId { get; set; } = string.Empty;
    /// <summary>Gets or sets a nonzero reproducible seed.</summary>
    [SerializableProperty] public uint seed { get; set; } = 1;
    /// <summary>Gets or sets elapsed logical seconds.</summary>
    [SerializableProperty] public double elapsed { get; set; }
    /// <summary>Gets or sets the emitter's horizontal placement.</summary>
    [SerializableProperty] public double x { get; set; }
    /// <summary>Gets or sets the emitter's vertical placement.</summary>
    [SerializableProperty] public double y { get; set; }
    /// <summary>Gets or sets whether emission has been explicitly stopped.</summary>
    [SerializableProperty] public bool stopped { get; set; }
    /// <summary>Gets or sets the logical time after which no new particles may be born.</summary>
    [SerializableProperty] public double emissionEnd { get; set; }
}

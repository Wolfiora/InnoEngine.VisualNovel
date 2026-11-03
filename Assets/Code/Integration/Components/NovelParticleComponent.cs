using System;
using InnoEngine.Scene;
using InnoEngine.Serialization;
using InnoEngine.Reflection;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Stage.Effects;

namespace Inno.VisualNovel.Integration.Components;

/// <summary>Owns one native particle emitter; individual particles remain deterministic drawing data.</summary>
[StableTypeId("b0f32c52-b546-4259-9e9e-d4cd7ebc9c04")]
public sealed class NovelParticleComponent : GameComponent
{
    /// <summary>Gets or sets the imported particle definition.</summary>
    [SerializableProperty] public VisualEffectAsset? preset { get; set; }
    /// <summary>Gets or sets whether this emitter is included in scene presentation.</summary>
    [SerializableProperty] public bool visible { get; set; } = true;
    /// <summary>Gets or sets whether the scene's runtime update advances the logical clock.</summary>
    [SerializableProperty] public bool playing { get; set; } = true;
    /// <summary>Gets or sets the reproducible nonzero particle seed.</summary>
    [SerializableProperty] public uint seed { get; set; } = 1;
    /// <summary>Gets or sets the logical clock, also usable for passive authoring scrubbing.</summary>
    [SerializableProperty] public double elapsed { get; set; }
    /// <summary>Gets or sets whether new births have stopped while existing particles finish.</summary>
    [SerializableProperty] public bool stopped { get; set; }
    /// <summary>Gets or sets the logical clock at which emission stopped.</summary>
    [SerializableProperty] public double emissionEnd { get; set; }

    /// <summary>Restarts emission without changing its native object transform or preset.</summary>
    public void Restart() { elapsed = emissionEnd = 0; stopped = false; playing = true; }
    /// <summary>Stops births while retaining already emitted particles.</summary>
    public void StopEmission() { if (!stopped) { emissionEnd = elapsed; stopped = true; } }
    /// <summary>Captures only simulation values; native Transform remains the sole spatial owner.</summary>
    /// <returns>An independent input for the pure particle evaluator.</returns>
    public ParticleEmitterState Capture()
    {
        var result = new ParticleEmitterState
        {
            id = identity.persistentId.ToString(), presetId = preset?.resourceId ?? string.Empty,
            seed = seed, elapsed = elapsed, stopped = stopped, emissionEnd = emissionEnd
        };
        ParticleSimulator.ValidateState(result);
        return result;
    }
    /// <summary>Advances a validated logical interval without touching graphics or spawning objects.</summary>
    /// <param name="seconds">Finite nonnegative runtime elapsed seconds.</param>
    public void Tick(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0 || !double.IsFinite(elapsed + seconds) || elapsed + seconds > 1e12)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        if (!playing || !visible || !gameObject.activeInHierarchy || preset is null) return;
        _ = Capture(); elapsed += seconds;
    }
}

using InnoEngine.Rendering;
using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Stages;

namespace Inno.VisualNovel.Model.Performance;

/// <summary>Declares a deterministic sprite emitter evaluated without incremental simulation state.</summary>
[RequiresSerializationConverter]
public sealed class ParticlePreset : ISerializable
{
    /// <summary>Gets or sets the optional imported sprite; null draws a solid quad.</summary>
    [SerializableProperty] public TextureAsset? texture { get; set; }
    /// <summary>Gets or sets the stage compositing group.</summary>
    [SerializableProperty] public StageLayerGroup group { get; set; } = StageLayerGroup.Foreground;
    /// <summary>Gets or sets composition order within the group.</summary>
    [SerializableProperty] public int order { get; set; }
    /// <summary>Gets or sets camera translation response; zero ignores camera translation.</summary>
    [SerializableProperty] public double parallax { get; set; }
    /// <summary>Gets or sets emission rectangle width.</summary>
    [SerializableProperty] public double width { get; set; } = 1920;
    /// <summary>Gets or sets emission rectangle height.</summary>
    [SerializableProperty] public double height { get; set; } = 100;
    /// <summary>Gets or sets particles emitted per logical second.</summary>
    [SerializableProperty] public double rate { get; set; } = 20;
    /// <summary>Gets or sets emission duration; zero emits until explicitly stopped.</summary>
    [SerializableProperty] public double duration { get; set; }
    /// <summary>Gets or sets the minimum randomized lifetime.</summary>
    [SerializableProperty] public double minimumLifetime { get; set; } = 2;
    /// <summary>Gets or sets the maximum randomized lifetime.</summary>
    [SerializableProperty] public double maximumLifetime { get; set; } = 4;
    /// <summary>Gets or sets the minimum horizontal velocity.</summary>
    [SerializableProperty] public double minimumVelocityX { get; set; } = -10;
    /// <summary>Gets or sets the maximum horizontal velocity.</summary>
    [SerializableProperty] public double maximumVelocityX { get; set; } = 10;
    /// <summary>Gets or sets the minimum vertical velocity.</summary>
    [SerializableProperty] public double minimumVelocityY { get; set; } = 70;
    /// <summary>Gets or sets the maximum vertical velocity.</summary>
    [SerializableProperty] public double maximumVelocityY { get; set; } = 120;
    /// <summary>Gets or sets constant horizontal acceleration.</summary>
    [SerializableProperty] public double accelerationX { get; set; }
    /// <summary>Gets or sets constant vertical acceleration.</summary>
    [SerializableProperty] public double accelerationY { get; set; }
    /// <summary>Gets or sets initial particle height.</summary>
    [SerializableProperty] public double startSize { get; set; } = 10;
    /// <summary>Gets or sets particle height at the end of its lifetime.</summary>
    [SerializableProperty] public double endSize { get; set; } = 4;
    /// <summary>Gets or sets particle width divided by height.</summary>
    [SerializableProperty] public double aspect { get; set; } = 1;
    /// <summary>Gets or sets the minimum randomized initial rotation in degrees.</summary>
    [SerializableProperty] public double minimumRotation { get; set; }
    /// <summary>Gets or sets the maximum randomized initial rotation in degrees.</summary>
    [SerializableProperty] public double maximumRotation { get; set; } = 360;
    /// <summary>Gets or sets maximum absolute randomized angular speed in degrees per second.</summary>
    [SerializableProperty] public double angularSpeed { get; set; } = 30;
    /// <summary>Gets or sets starting packed RRGGBBAA tint.</summary>
    [SerializableProperty] public uint startColor { get; set; } = 0xFFFFFFFF;
    /// <summary>Gets or sets final packed RRGGBBAA tint.</summary>
    [SerializableProperty] public uint endColor { get; set; } = 0xFFFFFF00;
    /// <summary>Gets or sets absolute height keys whose times denote normalized age; empty uses startSize and endSize.</summary>
    [SerializableProperty] public PerformanceKeyframe[] sizeKeys { get; set; } = [];
    /// <summary>Gets or sets lifetime color stops; empty uses startColor and endColor.</summary>
    [SerializableProperty] public ParticleColorKeyframe[] colorKeys { get; set; } = [];
    /// <summary>Gets or sets whether reduced-motion preferences may suppress this emitter.</summary>
    [SerializableProperty] public bool essential { get; set; }
}

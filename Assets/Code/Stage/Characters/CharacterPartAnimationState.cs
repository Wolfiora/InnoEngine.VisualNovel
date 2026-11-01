using System.Linq;
using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Characters;

namespace Inno.VisualNovel.Stage.Characters;

/// <summary>Freezes resolved part animation data independently of authoring assets.</summary>
[GenerateSerializationConverter]
public sealed class CharacterPartAnimationState : ISerializable
{
    /// <summary>Gets or sets the existing flattened layer identity.</summary>
    [SerializableProperty] public string layer { get; set; } = string.Empty;
    /// <summary>Gets or sets the pure playback mechanism.</summary>
    [SerializableProperty] public CharacterAnimationMode mode { get; set; }
    /// <summary>Gets or sets the resolved frame sequence.</summary>
    [SerializableProperty] public CharacterFrameState[] frames { get; set; } = [];
    /// <summary>Gets or sets whether an ordinary sequence repeats.</summary>
    [SerializableProperty] public bool loop { get; set; }
    /// <summary>Gets or sets the idle interval between blinks.</summary>
    [SerializableProperty] public double blinkInterval { get; set; }
    /// <summary>Gets or sets the frozen instance-based initial phase.</summary>
    [SerializableProperty] public double phase { get; set; }
    /// <summary>Gets or sets whether reduced motion preserves this sequence.</summary>
    [SerializableProperty] public bool essential { get; set; }
    /// <summary>Copies the complete neutral frame track.</summary>
    /// <returns>Independent frame and track data.</returns>
    public CharacterPartAnimationState Copy() => new()
    {
        layer = layer, mode = mode, loop = loop, blinkInterval = blinkInterval, phase = phase, essential = essential,
        frames = frames.Select(static frame => new CharacterFrameState { texture = frame.texture, seconds = frame.seconds }).ToArray()
    };
}

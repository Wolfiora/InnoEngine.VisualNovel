using System.Linq;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Characters;

/// <summary>Defines pure frame-selection rules independent of render frames and audio devices.</summary>
[GenerateSerializationConverter]
public sealed class CharacterPartAnimation : ISerializable
{
    /// <summary>Gets or sets the authored playback mechanism.</summary>
    [SerializableProperty] public CharacterAnimationMode mode { get; set; }
    /// <summary>Gets or sets the ordered images and per-image duration.</summary>
    [SerializableProperty] public CharacterAnimationFrame[] frames { get; set; } = [];
    /// <summary>Gets or sets whether an ordinary sequence repeats.</summary>
    [SerializableProperty] public bool loop { get; set; } = true;
    /// <summary>Gets or sets the idle time before each blink.</summary>
    [SerializableProperty] public double blinkInterval { get; set; } = 3.5;
    /// <summary>Gets or sets deterministic instance-based phase staggering for blinks and sequences.</summary>
    [SerializableProperty] public bool stagger { get; set; } = true;
    /// <summary>Gets or sets whether the frame animation remains active with reduced motion enabled.</summary>
    [SerializableProperty] public bool essential { get; set; }
    /// <summary>Copies every frame value without retaining editable aliases.</summary>
    /// <returns>An independently owned declaration.</returns>
    public CharacterPartAnimation Copy() => new()
    {
        mode = mode, loop = loop, blinkInterval = blinkInterval, stagger = stagger, essential = essential,
        frames = frames.Select(static item => new CharacterAnimationFrame { texture = item.texture, seconds = item.seconds }).ToArray()
    };
}

using InnoEngine.Serialization;

namespace Inno.VisualNovel.Player.Audio;

/// <summary>Captures audio policy independently of native mixer or voice handles.</summary>
[GenerateSerializationConverter]
public sealed class AudioPlaybackSnapshot : ISerializable
{
    /// <summary>Gets or sets active voices in stable submission order.</summary>
    [SerializableProperty] public AudioVoiceSnapshot[] voices { get; set; } = [];
    /// <summary>Gets or sets the current smooth voice-ducking multiplier.</summary>
    [SerializableProperty] public double duckGain { get; set; } = 1;
}

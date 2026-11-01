using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Story.Nodes.Audio;

/// <summary>Identifies a bounded declarative audio action.</summary>
public enum AudioOperation
{
    /// <summary>Replaces a logical slot, crossfading the previous sound.</summary>
    Play,
    /// <summary>Stops a slot using its cue's authored fade.</summary>
    Stop,
    /// <summary>Pauses all voices in a slot.</summary>
    Pause,
    /// <summary>Resumes paused voices in a slot.</summary>
    Resume,
    /// <summary>Waits until a logical slot has no remaining playback.</summary>
    Wait
}

/// <summary>Controls a stable audio slot without carrying native voice handles.</summary>
[GenerateSerializationConverter]
public sealed class AudioNode : ISerializable
{
    /// <summary>Gets or sets the requested action.</summary>
    [SerializableProperty] public AudioOperation operation { get; set; } = AudioOperation.Stop;
    /// <summary>Gets or sets the logical slot, such as bgm, ambience or voice.</summary>
    [SerializableProperty] public string slot { get; set; } = "voice";
    /// <summary>Gets or sets the explicit cue key required for Play.</summary>
    [SerializableProperty] public string cueId { get; set; } = string.Empty;
}

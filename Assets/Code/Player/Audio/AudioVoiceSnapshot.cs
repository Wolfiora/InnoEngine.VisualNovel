using InnoEngine.Serialization;

namespace Inno.VisualNovel.Player.Audio;

/// <summary>Captures one neutral voice and its unfinished linear fade.</summary>
[GenerateSerializationConverter]
public sealed class AudioVoiceSnapshot : ISerializable
{
    /// <summary>Gets or sets the logical replacement slot.</summary>
    [SerializableProperty] public string slot { get; set; } = string.Empty;
    /// <summary>Gets or sets the explicit audio resource key.</summary>
    [SerializableProperty] public string cueId { get; set; } = string.Empty;
    /// <summary>Gets or sets the dialogue character whose player-owned voice preference applies.</summary>
    [SerializableProperty] public string characterId { get; set; } = string.Empty;
    /// <summary>Gets or sets logical clip-relative playback seconds.</summary>
    [SerializableProperty] public double position { get; set; }
    /// <summary>Gets or sets the gain at the start of the current fade.</summary>
    [SerializableProperty] public double from { get; set; }
    /// <summary>Gets or sets the target authored gain.</summary>
    [SerializableProperty] public double to { get; set; }
    /// <summary>Gets or sets the elapsed fade duration.</summary>
    [SerializableProperty] public double fadeElapsed { get; set; }
    /// <summary>Gets or sets the full fade duration.</summary>
    [SerializableProperty] public double fadeDuration { get; set; }
    /// <summary>Gets or sets pause state.</summary>
    [SerializableProperty] public bool paused { get; set; }
    /// <summary>Gets or sets the active instance's loop policy, including gallery overrides.</summary>
    [SerializableProperty] public bool loop { get; set; }
    /// <summary>Gets or sets whether a zero-gain fade must release this voice.</summary>
    [SerializableProperty] public bool stopping { get; set; }
}

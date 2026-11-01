using InnoEngine.Serialization;

namespace Inno.VisualNovel.Stage.Effects;

/// <summary>Captures one screen-effect owner and its explicit logical fade clock.</summary>
[GenerateSerializationConverter]
public sealed class ScreenEffectState : ISerializable
{
    /// <summary>Gets or sets the session-local instance identity.</summary>
    [SerializableProperty] public string instanceId { get; set; } = string.Empty;
    /// <summary>Gets or sets the declared native preset identity.</summary>
    [SerializableProperty] public string resourceId { get; set; } = string.Empty;
    /// <summary>Gets or sets the initial normalized strength.</summary>
    [SerializableProperty] public double from { get; set; }
    /// <summary>Gets or sets the destination normalized strength.</summary>
    [SerializableProperty] public double to { get; set; }
    /// <summary>Gets or sets elapsed logical fade time.</summary>
    [SerializableProperty] public double elapsed { get; set; }
    /// <summary>Gets or sets the total logical fade time.</summary>
    [SerializableProperty] public double duration { get; set; }
    /// <summary>Gets or sets whether fade time is paused.</summary>
    [SerializableProperty] public bool paused { get; set; }
    /// <summary>Gets or sets whether UI is included in this compositing operation.</summary>
    [SerializableProperty] public bool includeUi { get; set; }
    /// <summary>Gets or sets explicit deterministic stack order.</summary>
    [SerializableProperty] public int order { get; set; }
}

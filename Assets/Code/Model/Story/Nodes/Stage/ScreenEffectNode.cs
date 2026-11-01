using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Story.Nodes.Stage;

/// <summary>Declares a screen-effect lifecycle action without graphics resources or callbacks.</summary>
[GenerateSerializationConverter]
public sealed class ScreenEffectNode : ISerializable
{
    /// <summary>Gets or sets the explicit lifecycle operation.</summary>
    [SerializableProperty] public ScreenEffectOperation operation { get; set; }
    /// <summary>Gets or sets the native preset key for Set and Fade.</summary>
    [SerializableProperty] public string resourceId { get; set; } = string.Empty;
    /// <summary>Gets or sets the stable owner-local effect identity.</summary>
    [SerializableProperty] public string instanceId { get; set; } = "screen";
    /// <summary>Gets or sets the normalized destination strength.</summary>
    [SerializableProperty] public double strength { get; set; } = 1;
    /// <summary>Gets or sets fade duration in logical seconds.</summary>
    [SerializableProperty] public double duration { get; set; } = 0.5;
    /// <summary>Gets or sets explicit effect-stack ordering.</summary>
    [SerializableProperty] public int order { get; set; }
    /// <summary>Gets or sets whether the operation includes game UI.</summary>
    [SerializableProperty] public bool includeUi { get; set; }
    /// <summary>Gets or sets whether Fade also inserts an explicit completion wait.</summary>
    [SerializableProperty] public bool waitForCompletion { get; set; }
}

/// <summary>Identifies deterministic screen-effect lifecycle operations.</summary>
public enum ScreenEffectOperation
{
    /// <summary>Sets an instance immediately.</summary>
    Set,
    /// <summary>Replaces an instance with a logical strength fade.</summary>
    Fade,
    /// <summary>Removes an instance immediately.</summary>
    Remove,
    /// <summary>Waits for an instance's current fade to finish.</summary>
    Wait,
    /// <summary>Pauses an in-progress fade.</summary>
    Pause,
    /// <summary>Resumes a paused fade.</summary>
    Resume,
    /// <summary>Finishes a fade at its declared destination strength.</summary>
    Complete
}

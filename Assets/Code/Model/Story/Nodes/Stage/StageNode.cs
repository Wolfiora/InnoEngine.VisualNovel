using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Stages;

namespace Inno.VisualNovel.Model.Story.Nodes.Stage;

/// <summary>Identifies implemented stage commands.</summary>
public enum StageOperation
{
    /// <summary>Replaces the scenery.</summary>
    SetStage,
    /// <summary>Shows or replaces one layered character.</summary>
    ShowCharacter,
    /// <summary>Removes one character instance.</summary>
    HideCharacter,
    /// <summary>Changes camera offset, zoom and rotation.</summary>
    SetCamera,
    /// <summary>Starts or replaces a deterministic particle emitter.</summary>
    StartParticles,
    /// <summary>Stops births while allowing live particles to expire.</summary>
    StopParticles,
    /// <summary>Immediately removes a particle emitter.</summary>
    RemoveParticles,
    /// <summary>Waits until the named emitter has no remaining births or live particles.</summary>
    WaitParticles,
    /// <summary>Begins authored mouth frames for a currently shown character instance.</summary>
    BeginSpeaking,
    /// <summary>Returns a character's mouth parts to their pose or expression image.</summary>
    EndSpeaking,
    /// <summary>Follows an existing qualified stage or character owner at a normalized screen anchor.</summary>
    FollowCamera,
    /// <summary>Stops following while freezing the current framing.</summary>
    StopCameraFollow
}

/// <summary>Declares a stage command resolved against the game's explicit resource library.</summary>
[GenerateSerializationConverter]
public sealed class StageNode : ISerializable
{
    /// <summary>Gets or sets the implemented operation.</summary>
    [SerializableProperty] public StageOperation operation { get; set; }
    /// <summary>Gets or sets the stable scenery or character resource key.</summary>
    [SerializableProperty] public string resourceId { get; set; } = string.Empty;
    /// <summary>Gets or sets the character instance key.</summary>
    [SerializableProperty] public string target { get; set; } = "actor";
    /// <summary>Gets or sets the pose key, empty for default.</summary>
    [SerializableProperty] public string pose { get; set; } = string.Empty;
    /// <summary>Gets or sets the expression key, empty for default.</summary>
    [SerializableProperty] public string expression { get; set; } = string.Empty;
    /// <summary>Gets or sets placement; camera following uses pivot fields as normalized screen focal anchors.</summary>
    [SerializableProperty] public StageTransform transform { get; set; } = new();
    /// <summary>Gets or sets character composition order.</summary>
    [SerializableProperty] public int order { get; set; }
    /// <summary>Gets or sets the nonzero particle seed, independent of frame timing.</summary>
    [SerializableProperty] public uint seed { get; set; } = 1;
}

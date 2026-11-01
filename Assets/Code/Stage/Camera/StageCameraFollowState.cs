using InnoEngine.Serialization;

namespace Inno.VisualNovel.Stage.Camera;

/// <summary>Stores the exact target owner and design-space focal anchor without retaining scene or asset objects.</summary>
[GenerateSerializationConverter]
public sealed class StageCameraFollowState : ISerializable
{
    /// <summary>Gets or sets the qualified existing stage or character owner.</summary>
    [SerializableProperty] public string owner { get; set; } = string.Empty;
    /// <summary>Gets or sets the desired horizontal viewport anchor from zero to one.</summary>
    [SerializableProperty] public double anchorX { get; set; } = .5;
    /// <summary>Gets or sets the desired vertical viewport anchor from zero to one.</summary>
    [SerializableProperty] public double anchorY { get; set; } = .5;
    /// <summary>Gets or sets the design canvas width used when following is explicitly stopped or its target disappears.</summary>
    [SerializableProperty] public double width { get; set; }
    /// <summary>Gets or sets the design canvas height used to freeze the last logical camera position.</summary>
    [SerializableProperty] public double height { get; set; }
    /// <summary>Copies neutral follow settings independently of later restore candidates.</summary>
    /// <returns>A detached state declaration.</returns>
    public StageCameraFollowState Copy() => new() { owner = owner, anchorX = anchorX, anchorY = anchorY, width = width, height = height };
}

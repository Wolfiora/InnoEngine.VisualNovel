using System.Linq;
using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Characters;

namespace Inno.VisualNovel.Stage.Characters;

/// <summary>Captures one character's declarative animation, speaking markers and logical origin.</summary>
[GenerateSerializationConverter]
public sealed class CharacterAnimationState : ISerializable
{
    /// <summary>Gets or sets the existing complete character owner.</summary>
    [SerializableProperty] public string owner { get; set; } = string.Empty;
    /// <summary>Gets or sets the originating character resource for diagnostics.</summary>
    [SerializableProperty] public string resource { get; set; } = string.Empty;
    /// <summary>Gets or sets the stage time at which the pose was shown.</summary>
    [SerializableProperty] public double started { get; set; }
    /// <summary>Gets or sets whether an explicit speaking marker is active.</summary>
    [SerializableProperty] public bool speaking { get; set; }
    /// <summary>Gets or sets the stage time at which the current speaking interval began.</summary>
    [SerializableProperty] public double speechStarted { get; set; }
    /// <summary>Gets or sets the independently copied optional breathing rule.</summary>
    [SerializableProperty] public CharacterBreathing breathing { get; set; } = new();
    /// <summary>Gets or sets the frozen instance-based breathing phase.</summary>
    [SerializableProperty] public double breathingPhase { get; set; }
    /// <summary>Gets or sets the breathing anchor in the character's local coordinates.</summary>
    [SerializableProperty] public double pivotX { get; set; }
    /// <summary>Gets or sets the breathing anchor's vertical coordinate.</summary>
    [SerializableProperty] public double pivotY { get; set; }
    /// <summary>Gets or sets the character-to-stage basis in row-vector affine order.</summary>
    [SerializableProperty] public double[] basis { get; set; } = [1, 0, 0, 1, 0, 0];
    /// <summary>Gets or sets independent animated part tracks.</summary>
    [SerializableProperty] public CharacterPartAnimationState[] parts { get; set; } = [];
    /// <summary>Copies all neutral data without retaining mutable arrays or source declarations.</summary>
    /// <returns>An independent snapshot.</returns>
    public CharacterAnimationState Copy() => new()
    {
        owner = owner, resource = resource, started = started, speaking = speaking, speechStarted = speechStarted,
        breathing = breathing.Copy(), breathingPhase = breathingPhase, pivotX = pivotX, pivotY = pivotY, basis = basis.ToArray(), parts = parts.Select(static part => part.Copy()).ToArray()
    };
}

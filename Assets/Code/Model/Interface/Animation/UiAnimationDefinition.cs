using System;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Interface.Animation;

/// <summary>Owns independent visibility, interaction and waiting presentation channels for one element.</summary>
[GenerateSerializationConverter]
public sealed class UiAnimationDefinition : ISerializable
{
    /// <summary>Gets or sets the hidden-to-ordinary animation on page entry or conditional reveal.</summary>
    [SerializableProperty] public UiMotionDefinition enter { get; set; } = new();
    /// <summary>Gets or sets the ordinary-to-hidden animation on page exit or conditional hiding.</summary>
    [SerializableProperty] public UiMotionDefinition exit { get; set; } = new();
    /// <summary>Gets or sets the reversible pointer-hover state animation.</summary>
    [SerializableProperty] public UiMotionDefinition hover { get; set; } = new();
    /// <summary>Gets or sets the reversible keyboard-focus state animation.</summary>
    [SerializableProperty] public UiMotionDefinition focus { get; set; } = new();
    /// <summary>Gets or sets the reversible held-pointer state animation.</summary>
    [SerializableProperty] public UiMotionDefinition pressed { get; set; } = new();
    /// <summary>Gets or sets the continuously reversing animation while the waiting condition is true.</summary>
    [SerializableProperty] public UiMotionDefinition waiting { get; set; } = new();
    /// <summary>Gets or sets an optional Boolean field enabling waiting motion; empty means continuously active.</summary>
    [SerializableProperty] public string waitingBinding { get; set; } = string.Empty;
    /// <summary>Validates every owned channel before runtime state is created.</summary>
    public void Validate()
    {
        enter.Validate(); exit.Validate(); hover.Validate(); focus.Validate(); pressed.Validate(); waiting.Validate();
    }
    /// <summary>Copies the full declaration without sharing mutable channels.</summary>
    /// <returns>An independent animation definition.</returns>
    public UiAnimationDefinition Copy() => new()
    {
        enter = enter.Copy(), exit = exit.Copy(), hover = hover.Copy(), focus = focus.Copy(), pressed = pressed.Copy(), waiting = waiting.Copy(), waitingBinding = waitingBinding
    };
    /// <summary>Returns the owned declaration for an explicitly selected presentation event.</summary>
    /// <param name="kind">The event selected by an author or preview host.</param>
    /// <returns>The mutable declaration belonging to this animation definition.</returns>
    public UiMotionDefinition GetMotion(UiAnimationEvent kind) => kind switch
    {
        UiAnimationEvent.Enter => enter, UiAnimationEvent.Exit => exit, UiAnimationEvent.Hover => hover,
        UiAnimationEvent.Focus => focus, UiAnimationEvent.Pressed => pressed, UiAnimationEvent.Waiting => waiting,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}

using System;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Interface.Animation;

/// <summary>Declares a side-effect-free transition between the ordinary pose and a relative pose.</summary>
[GenerateSerializationConverter]
public sealed class UiMotionDefinition : ISerializable
{
    /// <summary>Gets or sets whether this event animates presentation.</summary>
    [SerializableProperty] public bool enabled { get; set; }
    /// <summary>Gets or sets travel duration in logical seconds; zero applies the destination immediately.</summary>
    [SerializableProperty] public double duration { get; set; } = .18;
    /// <summary>Gets or sets the initial hold before travel, in logical seconds.</summary>
    [SerializableProperty] public double delay { get; set; }
    /// <summary>Gets or sets the timing function.</summary>
    [SerializableProperty] public UiMotionEasing easing { get; set; } = UiMotionEasing.Smooth;
    /// <summary>Gets or sets the hidden or active-state relative pose.</summary>
    [SerializableProperty] public UiMotionPose pose { get; set; } = new();
    /// <summary>Validates timing and channel limits even when the declaration is disabled.</summary>
    public void Validate()
    {
        if (!double.IsFinite(duration) || duration is < 0 or > 30 || !double.IsFinite(delay) || delay is < 0 or > 30 || !Enum.IsDefined(easing))
            throw new InvalidOperationException("UI motion duration and delay must be finite values between zero and thirty seconds.");
        if (pose is null) throw new InvalidOperationException("UI motion requires a pose declaration.");
        pose.Validate();
    }
    /// <summary>Creates a detached timing and channel declaration.</summary>
    /// <returns>A copy without mutable aliases.</returns>
    public UiMotionDefinition Copy() => new() { enabled = enabled, duration = duration, delay = delay, easing = easing, pose = pose.Copy() };
}

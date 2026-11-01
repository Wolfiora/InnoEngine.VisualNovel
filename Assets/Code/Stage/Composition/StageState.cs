using System;
using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Stage.Timeline;
using Inno.VisualNovel.Stage.Effects;
using Inno.VisualNovel.Stage.Characters;

namespace Inno.VisualNovel.Stage.Composition;

/// <summary>Stores the complete neutral two-dimensional composition and camera state.</summary>
[GenerateSerializationConverter]
public sealed class StageState : ISerializable
{
    /// <summary>Gets or sets flattened image and character-part states.</summary>
    [SerializableProperty] public StageLayerState[] layers { get; set; } = [];
    /// <summary>Gets or sets pure character frame, breathing and speaking state.</summary>
    [SerializableProperty] public CharacterAnimationState[] characters { get; set; } = [];
    /// <summary>Gets or sets camera horizontal offset.</summary>
    [SerializableProperty] public double cameraX { get; set; }
    /// <summary>Gets or sets camera vertical offset.</summary>
    [SerializableProperty] public double cameraY { get; set; }
    /// <summary>Gets or sets positive camera zoom.</summary>
    [SerializableProperty] public double cameraZoom { get; set; } = 1;
    /// <summary>Gets or sets camera rotation.</summary>
    [SerializableProperty] public double cameraRotation { get; set; }
    /// <summary>Gets or sets optional owner-qualified following and its design-space focal anchor.</summary>
    [SerializableProperty] public Camera.StageCameraFollowState? cameraFollow { get; set; }
    /// <summary>Gets or sets deterministic logical time.</summary>
    [SerializableProperty] public double time { get; set; }
    /// <summary>Gets or sets active and completed timeline property owners.</summary>
    [SerializableProperty] public StagePerformanceState[] performances { get; set; } = [];
    /// <summary>Gets or sets deterministic particle emitters and their logical times.</summary>
    [SerializableProperty] public ParticleEmitterState[] particles { get; set; } = [];
    /// <summary>Gets or sets persistent screen-effect instances and fade clocks.</summary>
    [SerializableProperty] public ScreenEffectState[] screenEffects { get; set; } = [];
    /// <summary>Gets or sets zero or one active composition boundaries.</summary>
    [SerializableProperty] public Transitions.StageTransitionState[] transitions { get; set; } = [];
    /// <summary>Gets or sets the monotonic boundary identity used to associate UI snapshots.</summary>
    [SerializableProperty] public long transitionSequence { get; set; }
}

using System;
using System.Collections.Generic;
using InnoEngine.Scene;
using InnoEngine.Serialization;
using InnoEngine.Reflection;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Stage.Effects;

namespace Inno.VisualNovel.Integration.Components;

/// <summary>Owns a native scene's post-processing contribution and its explicit fade clock.</summary>
[StableTypeId("b0f32c52-b546-4259-9e9e-d4cd7ebc9c05")]
public sealed class NovelScreenEffectComponent : GameComponent
{
    /// <summary>Gets or sets the imported screen effect.</summary>
    [SerializableProperty] public VisualEffectAsset? preset { get; set; }
    /// <summary>Gets or sets instance-specific parameters; null inherits the assigned preset without modifying it.</summary>
    [SerializableProperty] public ScreenEffectDefinition? parameters { get; set; }
    /// <summary>Gets or sets whether this component contributes an image operation.</summary>
    [SerializableProperty] public bool visible { get; set; } = true;
    /// <summary>Gets or sets the fade's starting normalized strength.</summary>
    [SerializableProperty] public double from { get; set; } = 1;
    /// <summary>Gets or sets the fade's ending normalized strength.</summary>
    [SerializableProperty] public double strength { get; set; } = 1;
    /// <summary>Gets or sets the total fade duration; zero applies the final strength immediately.</summary>
    [SerializableProperty] public double duration { get; set; }
    /// <summary>Gets or sets the elapsed fade clock, also usable for passive scrubbing.</summary>
    [SerializableProperty] public double elapsed { get; set; }
    /// <summary>Gets or sets whether runtime updates retain the current fade time.</summary>
    [SerializableProperty] public bool paused { get; set; }
    /// <summary>Gets or sets whether the operation includes game UI.</summary>
    [SerializableProperty] public bool includeUi { get; set; }
    /// <summary>Gets or sets explicit compositing order; native hierarchy resolves equal values.</summary>
    [SerializableProperty] public int order { get; set; }

    /// <summary>Evaluates current component values using the shared screen-effect rules without retaining an effect tree.</summary>
    /// <param name="allowFlashing">The player's accessibility policy.</param>
    /// <returns>Detached operations, or none when this component has no visible preset.</returns>
    public IReadOnlyList<ScreenEffectFrame> Capture(bool allowFlashing)
    {
        if (!visible || !gameObject.activeInHierarchy || preset is null) return [];
        var evaluator = new ScreenEffectController(preset.resourceId, CaptureParameters());
        evaluator.Restore([new ScreenEffectState
        {
            instanceId = identity.persistentId.ToString(), resourceId = preset.resourceId,
            from = from, to = strength, duration = duration, elapsed = elapsed,
            paused = paused, includeUi = includeUi, order = order
        }]);
        return evaluator.BuildFrame(allowFlashing);
    }
    /// <summary>Copies this instance's effective image parameters, rejecting an incompatible override.</summary>
    /// <returns>Independent validated values suitable for authoring, timeline evaluation and snapshots.</returns>
    public ScreenEffectDefinition CaptureParameters()
    {
        if (preset is null || preset.kind != VisualEffectKind.Screen)
            throw new InvalidOperationException("Assign a screen-effect preset before editing its parameters.");
        var result = parameters ?? preset.screen;
        if (result.kind != preset.screen.kind)
            throw new InvalidOperationException("The instance parameters use a different screen-effect mechanism. Reset the override after changing the preset.");
        VisualEffectRules.Validate(result);
        return result.Copy();
    }
    /// <summary>Advances the logical fade clock during native runtime updates only.</summary>
    /// <param name="seconds">Finite nonnegative runtime elapsed seconds.</param>
    public void Tick(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (!paused && visible && gameObject.activeInHierarchy && preset is not null)
        {
            _ = Capture(true); elapsed = Math.Min(duration, elapsed + seconds);
        }
    }
}

using InnoEngine.Scene;
using InnoEngine.Serialization;
using InnoEngine.Reflection;

namespace Inno.VisualNovel.Integration.Components;

/// <summary>Declares a scene-local story target without replacing native object identity.</summary>
[StableTypeId("b0f32c52-b546-4259-9e9e-d4cd7ebc9c02")]
public sealed class NovelTargetComponent : GameComponent
{
    /// <summary>Gets or sets the explicit story binding key; duplicate nonempty keys are invalid.</summary>
    [SerializableProperty] public string targetId { get; set; } = string.Empty;
}

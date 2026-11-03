using InnoEngine.Reflection;
using InnoEngine.Scene;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Integration.Components.Interface;

/// <summary>Owns a Boolean value on a real control, optionally redirected through a typed binding.</summary>
[StableTypeId("b0f32c52-b546-4259-9e9e-d4cd7ebc9c2a")]
public sealed class NovelUiToggleComponent : GameComponent
{
    /// <summary>Gets or sets the component-owned value used when no data binding is assigned.</summary>
    [SerializableProperty] public bool value { get; set; }
    /// <summary>Gets or sets whether this toggle accepts interaction.</summary>
    [SerializableProperty] public bool interactable { get; set; } = true;
}

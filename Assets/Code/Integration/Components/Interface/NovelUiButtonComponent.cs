using InnoEngine.Reflection;
using InnoEngine.Scene;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Integration.Components.Interface;

/// <summary>Makes an object's resolved UI rectangle a focusable button; actions are declared by a separate component.</summary>
[StableTypeId("b0f32c52-b546-4259-9e9e-d4cd7ebc9c27")]
public sealed class NovelUiButtonComponent : GameComponent
{
    /// <summary>Gets or sets whether pointer or keyboard activation is accepted.</summary>
    [SerializableProperty] public bool interactable { get; set; } = true;
}

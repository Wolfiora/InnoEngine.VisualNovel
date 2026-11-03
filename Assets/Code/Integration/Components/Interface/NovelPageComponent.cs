using InnoEngine.Reflection;
using InnoEngine.Scene;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Integration.Components.Interface;

/// <summary>Declares the role and input ownership of a page made from actual descendant controls.</summary>
[StableTypeId("b0f32c52-b546-4259-9e9e-d4cd7ebc9c26")]
public sealed class NovelPageComponent : GameComponent
{
    /// <summary>Gets or sets the game-shell role, such as title, reading or settings.</summary>
    [SerializableProperty] public string role { get; set; } = "reading";
    /// <summary>Gets or sets whether this active page blocks input to pages and story beneath it.</summary>
    [SerializableProperty] public bool modal { get; set; }
    /// <summary>Gets or sets the real control that should initially receive keyboard focus.</summary>
    [SerializableProperty] public GameObject? initialFocus { get; set; }
}

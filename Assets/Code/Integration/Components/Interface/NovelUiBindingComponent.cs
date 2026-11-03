using InnoEngine.Reflection;
using InnoEngine.Scene;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Integration.Components.Interface;

/// <summary>Binds actual UI components to typed game data without replacing their native ownership.</summary>
[StableTypeId("b0f32c52-b546-4259-9e9e-d4cd7ebc9c29")]
public sealed class NovelUiBindingComponent : GameComponent
{
    /// <summary>Gets or sets the displayed text path; empty uses the text component's literal.</summary>
    [SerializableProperty] public string text { get; set; } = string.Empty;
    /// <summary>Gets or sets the imported image field path.</summary>
    [SerializableProperty] public string image { get; set; } = string.Empty;
    /// <summary>Gets or sets a Boolean condition for this object's complete UI subtree.</summary>
    [SerializableProperty] public string visible { get; set; } = string.Empty;
    /// <summary>Gets or sets a Boolean interaction condition, in addition to native activation.</summary>
    [SerializableProperty] public string interactable { get; set; } = string.Empty;
    /// <summary>Gets or sets the typed value path for a toggle, slider or other value control.</summary>
    [SerializableProperty] public string value { get; set; } = string.Empty;
}

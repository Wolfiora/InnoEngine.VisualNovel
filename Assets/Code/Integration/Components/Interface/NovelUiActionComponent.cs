using InnoEngine.Reflection;
using InnoEngine.Scene;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Integration.Components.Interface;

/// <summary>Declares a neutral game action independently of button visuals or input device.</summary>
[StableTypeId("b0f32c52-b546-4259-9e9e-d4cd7ebc9c28")]
public sealed class NovelUiActionComponent : GameComponent
{
    /// <summary>Gets or sets the registered system or extension action identity.</summary>
    [SerializableProperty] public string action { get; set; } = "advance";
    /// <summary>Gets or sets a literal argument when no typed binding is assigned.</summary>
    [SerializableProperty] public string argument { get; set; } = string.Empty;
    /// <summary>Gets or sets a typed argument data path; empty retains the literal argument.</summary>
    [SerializableProperty] public string argumentBinding { get; set; } = string.Empty;
}

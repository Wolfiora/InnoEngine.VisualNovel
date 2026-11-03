using InnoEngine.Reflection;
using InnoEngine.Scene;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Integration.Components.Interface;

/// <summary>Declares numeric input on a real object; projection requires engine value-change payloads unavailable in the selected SDK.</summary>
[StableTypeId("b0f32c52-b546-4259-9e9e-d4cd7ebc9c2b")]
public sealed class NovelUiSliderComponent : GameComponent
{
    /// <summary>Gets or sets the component-owned value used when no data binding is assigned.</summary>
    [SerializableProperty] public double value { get; set; }
    /// <summary>Gets or sets the lower bound.</summary>
    [SerializableProperty] public double minimum { get; set; }
    /// <summary>Gets or sets the upper bound.</summary>
    [SerializableProperty] public double maximum { get; set; } = 1;
    /// <summary>Gets or sets the requested discrete increment; zero declares a continuous value.</summary>
    [SerializableProperty] public double step { get; set; } = .05;
    /// <summary>Gets or sets whether this slider accepts interaction.</summary>
    [SerializableProperty] public bool interactable { get; set; } = true;
}

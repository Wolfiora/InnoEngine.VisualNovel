using InnoEngine.Reflection;
using InnoEngine.Scene;
using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Interface;

namespace Inno.VisualNovel.Integration.Components.Interface;

/// <summary>Allocates layout slots to actual child objects without instantiating or retaining a separate control tree.</summary>
[StableTypeId("b0f32c52-b546-4259-9e9e-d4cd7ebc9c22")]
public sealed class NovelUiContainerComponent : GameComponent
{
    /// <summary>Gets or sets the automatic arrangement of native children.</summary>
    [SerializableProperty] public UiContainerLayout layout { get; set; }
    /// <summary>Gets or sets the container inset in design units.</summary>
    [SerializableProperty] public float padding { get; set; }
    /// <summary>Gets or sets spacing between automatic slots.</summary>
    [SerializableProperty] public float gap { get; set; } = 12;
    /// <summary>Gets or sets grid column count.</summary>
    [SerializableProperty] public int columns { get; set; } = 2;
    /// <summary>Gets or sets distribution of unused main-axis space.</summary>
    [SerializableProperty] public UiFlowAlignment alignment { get; set; }
}

using InnoEngine.Reflection;
using InnoEngine.Scene;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Integration.Components.Interface;

/// <summary>Delegates clipping, wheel scrolling and scrollbars to the engine UI context for a real container.</summary>
[StableTypeId("b0f32c52-b546-4259-9e9e-d4cd7ebc9c2d")]
public sealed class NovelUiScrollComponent : GameComponent
{
    /// <summary>Gets or sets whether content may scroll horizontally.</summary>
    [SerializableProperty] public bool horizontal { get; set; }
    /// <summary>Gets or sets whether content may scroll vertically.</summary>
    [SerializableProperty] public bool vertical { get; set; } = true;
}

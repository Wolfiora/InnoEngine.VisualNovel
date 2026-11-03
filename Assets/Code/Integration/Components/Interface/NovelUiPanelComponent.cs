using InnoEngine.Reflection;
using InnoEngine.Scene;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Integration.Components.Interface;

/// <summary>Paints the native object's layout rectangle below its image, text and descendants.</summary>
[StableTypeId("b0f32c52-b546-4259-9e9e-d4cd7ebc9c23")]
public sealed class NovelUiPanelComponent : GameComponent
{
    /// <summary>Gets or sets the RRGGBBAA panel fill.</summary>
    [SerializableProperty] public uint color { get; set; } = 0x202534FF;
    /// <summary>Gets or sets whether this panel consumes pointer input before controls behind it.</summary>
    [SerializableProperty] public bool blocksPointer { get; set; } = true;
}

using InnoEngine.Reflection;
using InnoEngine.Scene;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Integration.Components.Interface;

/// <summary>Provides interaction-state colors independently of button, toggle or slider behavior.</summary>
[StableTypeId("b0f32c52-b546-4259-9e9e-d4cd7ebc9c2c")]
public sealed class NovelUiControlStyleComponent : GameComponent
{
    /// <summary>Gets or sets the hovered fill.</summary>
    [SerializableProperty] public uint hover { get; set; } = 0x343E55FF;
    /// <summary>Gets or sets the pressed fill.</summary>
    [SerializableProperty] public uint pressed { get; set; } = 0x44597AFF;
    /// <summary>Gets or sets the disabled fill.</summary>
    [SerializableProperty] public uint disabled { get; set; } = 0x242630FF;
    /// <summary>Gets or sets the keyboard focus outline.</summary>
    [SerializableProperty] public uint focus { get; set; } = 0x93BFFFFF;
}

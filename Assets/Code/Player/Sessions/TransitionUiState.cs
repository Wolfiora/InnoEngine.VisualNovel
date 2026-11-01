using InnoEngine.Serialization;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Player.Sessions;

/// <summary>Associates a detached previous UI image with one exact output-transition boundary.</summary>
[GenerateSerializationConverter]
public sealed class TransitionUiState : ISerializable
{
    /// <summary>Gets or sets the stage boundary identity, zero for no captured output.</summary>
    [SerializableProperty] public long sequence { get; set; }
    /// <summary>Gets or sets the previous neutral UI image.</summary>
    [SerializableProperty] public UiFrameSnapshot frame { get; set; } = new();
}

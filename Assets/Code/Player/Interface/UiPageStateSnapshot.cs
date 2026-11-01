using InnoEngine.Serialization;
using Inno.VisualNovel.Narrative.Values;

namespace Inno.VisualNovel.Player.Interface;

/// <summary>Contains only the explicitly game-persistent fields of one authored shell role.</summary>
[GenerateSerializationConverter]
public sealed class UiPageStateSnapshot : ISerializable
{
    /// <summary>Gets or sets the stable shell role, independent of the selected visual theme.</summary>
    [SerializableProperty] public string role { get; set; } = string.Empty;
    /// <summary>Gets or sets a complete detached set of Game-scoped page fields.</summary>
    [SerializableProperty] public VariableState[] values { get; set; } = [];
}

using InnoEngine.Input;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Player.Shell.Keyboard;

/// <summary>Assigns one player-owned reading or menu action to a formal engine key without replacing viewport input ownership.</summary>
[GenerateSerializationConverter]
public sealed class NovelKeyboardBinding : ISerializable
{
    /// <summary>Gets or sets one supported shell action identity.</summary>
    [SerializableProperty] public string action { get; set; } = string.Empty;
    /// <summary>Gets or sets the engine key; Unknown explicitly disables this action's keyboard shortcut.</summary>
    [SerializableProperty] public KeyCode key { get; set; }
    /// <summary>Gets or sets the exact formal modifier combination; extra modifiers do not match this shortcut.</summary>
    [SerializableProperty] public KeyModifier modifiers { get; set; }
    /// <summary>Copies neutral mapping values independently of player settings storage.</summary>
    /// <returns>An editable mapping with the same semantic action.</returns>
    public NovelKeyboardBinding Copy() => new() { action = action, key = key, modifiers = modifiers };
}

using InnoEngine.Serialization;

namespace Inno.VisualNovel.Player.Shell;

/// <summary>Stores one player's independent character voice policy outside story snapshots.</summary>
[GenerateSerializationConverter]
public sealed class CharacterVoicePreference : ISerializable
{
    /// <summary>Gets or sets the stable character resource identity.</summary>
    [SerializableProperty] public string characterId { get; set; } = string.Empty;
    /// <summary>Gets or sets the linear gain before category and master mixing.</summary>
    [SerializableProperty] public double volume { get; set; } = 1;
    /// <summary>Gets or sets whether this character's voice is audible.</summary>
    [SerializableProperty] public bool enabled { get; set; } = true;
}

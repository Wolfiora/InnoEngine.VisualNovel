using InnoEngine.Serialization;
using Inno.VisualNovel.Player.Shell;

namespace Inno.VisualNovel.Player.Persistence;

/// <summary>Owns a game's player settings independently of progress and ordinary saves.</summary>
[GenerateSerializationConverter]
public sealed class PlayerPreferencesState : ISerializable
{
    /// <summary>Gets or sets the stable game identity.</summary>
    [SerializableProperty] public string gameId { get; set; } = string.Empty;
    /// <summary>Gets or sets validated player-owned settings.</summary>
    [SerializableProperty] public PlayerPreferences preferences { get; set; } = new();
}

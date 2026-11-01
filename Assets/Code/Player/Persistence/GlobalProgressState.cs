using InnoEngine.Serialization;
using Inno.VisualNovel.Narrative.Values;

namespace Inno.VisualNovel.Player.Persistence;

/// <summary>Separates monotonic reading progress and declared globals from ordinary saves.</summary>
[GenerateSerializationConverter]
public sealed class GlobalProgressState : ISerializable
{
    /// <summary>Gets or sets the stable game identity.</summary>
    [SerializableProperty] public string gameId { get; set; } = string.Empty;
    /// <summary>Gets or sets content-addressed read lines.</summary>
    [SerializableProperty] public string[] readContent { get; set; } = [];
    /// <summary>Gets or sets unlocked collection identities independently of ordinary game saves.</summary>
    [SerializableProperty] public string[] unlocked { get; set; } = [];
    /// <summary>Gets or sets the declared global variable scope.</summary>
    [SerializableProperty] public VariableState[] variables { get; set; } = [];
}

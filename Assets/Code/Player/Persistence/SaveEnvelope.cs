using InnoEngine.Serialization;
using Inno.VisualNovel.Player.Sessions;

namespace Inno.VisualNovel.Player.Persistence;

/// <summary>Commits metadata, thumbnail and game state in one native storage value.</summary>
[GenerateSerializationConverter]
public sealed class SaveEnvelope : ISerializable
{
    /// <summary>Gets or sets the slot category.</summary>
    [SerializableProperty] public SaveSlotKind kind { get; set; }
    /// <summary>Gets or sets the zero-based slot number.</summary>
    [SerializableProperty] public int index { get; set; }
    /// <summary>Gets or sets UTC ticks at snapshot creation.</summary>
    [SerializableProperty] public long utcTicks { get; set; }
    /// <summary>Gets or sets the complete game snapshot.</summary>
    [SerializableProperty] public NovelSessionState state { get; set; } = new();
    /// <summary>Gets or sets an encoded thumbnail captured with this state.</summary>
    [SerializableProperty] public byte[] thumbnail { get; set; } = [];
}

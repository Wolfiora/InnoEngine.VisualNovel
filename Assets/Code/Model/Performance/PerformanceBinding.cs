using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Performance;

/// <summary>Maps a reusable clip target slot onto a concrete stage owner.</summary>
[GenerateSerializationConverter]
public sealed class PerformanceBinding : ISerializable
{
    /// <summary>Gets or sets the target slot declared in the clip.</summary>
    [SerializableProperty] public string slot { get; set; } = string.Empty;
    /// <summary>Gets or sets a concrete stage owner or the reserved camera target.</summary>
    [SerializableProperty] public string target { get; set; } = string.Empty;
}

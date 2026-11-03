using InnoEngine.Scene;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Integration.Components;

/// <summary>Binds a reusable clip slot to one native object in the owning scene.</summary>
[GenerateSerializationConverter]
public sealed class NovelPerformanceBinding : ISerializable
{
    /// <summary>Gets or sets the exact track slot declared by the clip.</summary>
    [SerializableProperty] public string slot { get; set; } = string.Empty;
    /// <summary>Gets or sets the native target; deleted objects remain missing rather than resolving by name.</summary>
    [SerializableProperty] public GameObject? target { get; set; }
}

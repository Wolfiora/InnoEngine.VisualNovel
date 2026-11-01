using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Story.Nodes.Flow;

/// <summary>Suspends execution for logical time or an explicit player advance without creating dialogue.</summary>
[GenerateSerializationConverter]
public sealed class WaitNode : ISerializable
{
    /// <summary>Gets or sets whether a player advance is required instead of using the authored duration.</summary>
    [SerializableProperty] public bool waitForInput { get; set; }
    /// <summary>Gets or sets the duration in logical seconds.</summary>
    [SerializableProperty]
    public double seconds { get; set; } = 1;
}

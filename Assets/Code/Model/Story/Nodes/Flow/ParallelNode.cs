using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Story.Nodes.Flow;

/// <summary>Starts bounded presentation branches that reconverge at one explicitly owned join.</summary>
[GenerateSerializationConverter]
public sealed class ParallelNode : ISerializable
{
    /// <summary>Gets or sets the stable matching join node.</summary>
    [SerializableProperty] public string joinId { get; set; } = string.Empty;
    /// <summary>Gets or sets stable branch ports in deterministic submission order.</summary>
    [SerializableProperty] public string[] branches { get; set; } = ["first", "second"];
    /// <summary>Gets or sets whether the first completed task resumes the parent and cancels the rest.</summary>
    [SerializableProperty] public bool waitAny { get; set; }
}

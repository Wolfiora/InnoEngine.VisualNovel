using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Story.Nodes.Flow;

/// <summary>Terminates a parallel child while the owning parent resumes through this node's next edge.</summary>
[GenerateSerializationConverter]
public sealed class JoinNode : ISerializable
{
    /// <summary>Gets or sets the stable matching fork node.</summary>
    [SerializableProperty] public string forkId { get; set; } = string.Empty;
}

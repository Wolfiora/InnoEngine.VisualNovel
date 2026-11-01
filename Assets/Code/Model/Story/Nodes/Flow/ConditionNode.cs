using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Model.Story.Nodes.Flow;

/// <summary>Branches on one typed Boolean expression.</summary>
[GenerateSerializationConverter]
public sealed class ConditionNode : ISerializable
{
    /// <summary>Gets or sets the Boolean condition.</summary>
    [SerializableProperty]
    public ExpressionDocument condition { get; set; } = new();
}

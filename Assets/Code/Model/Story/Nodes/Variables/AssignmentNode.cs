using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Model.Story.Nodes.Variables;

/// <summary>Assigns a typed expression result to a declared variable.</summary>
[GenerateSerializationConverter]
public sealed class AssignmentNode : ISerializable
{
    /// <summary>Gets or sets the destination stable variable identity.</summary>
    [SerializableProperty]
    public string variableId { get; set; } = string.Empty;
    /// <summary>Gets or sets the value expression.</summary>
    [SerializableProperty]
    public ExpressionDocument value { get; set; } = new();
}

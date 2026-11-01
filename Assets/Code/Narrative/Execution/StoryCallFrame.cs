using InnoEngine.Serialization;
using Inno.VisualNovel.Narrative.Values;

namespace Inno.VisualNovel.Narrative.Execution;

/// <summary>Stores one invocation's return address and neutral local values.</summary>
[GenerateSerializationConverter]
public sealed class StoryCallFrame : ISerializable
{
    /// <summary>Gets or sets the stable return node.</summary>
    [SerializableProperty]
    public string returnNode { get; set; } = string.Empty;
    /// <summary>Gets or sets caller-local values.</summary>
    [SerializableProperty]
    public VariableState[] locals { get; set; } = [];
}

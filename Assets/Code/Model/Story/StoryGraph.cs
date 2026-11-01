using InnoEngine.Graphs;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Story;

/// <summary>Owns the single neutral control-flow graph of a story asset.</summary>
[RequiresSerializationConverter]
public sealed class StoryGraph : ISerializable
{
    /// <summary>Gets the authoritative editable graph; semantic edits use the authoring service.</summary>
    public GraphDocument document { get; } = new();
}

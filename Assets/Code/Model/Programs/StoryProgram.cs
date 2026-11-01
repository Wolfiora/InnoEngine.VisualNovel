using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Model.Programs;

/// <summary>Stores a compiled chapter consumed directly by runtime and preview sessions.</summary>
[GenerateSerializationConverter]
public sealed class StoryProgram : ISerializable
{
    /// <summary>Gets or sets the chapter semantic identity.</summary>
    [SerializableProperty]
    public string chapterId { get; set; } = string.Empty;
    /// <summary>Gets or sets the exact program content fingerprint.</summary>
    [SerializableProperty]
    public string fingerprint { get; set; } = string.Empty;
    /// <summary>Gets or sets lowered instructions.</summary>
    [SerializableProperty]
    public StoryInstruction[] instructions { get; set; } = [];
    /// <summary>Gets or sets stable public entry mappings.</summary>
    [SerializableProperty]
    public StoryEntry[] entries { get; set; } = [];
    /// <summary>Gets or sets declared typed variables.</summary>
    [SerializableProperty]
    public VariableDefinition[] variables { get; set; } = [];
}

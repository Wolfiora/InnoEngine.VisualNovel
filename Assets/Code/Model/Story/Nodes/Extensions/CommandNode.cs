using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Extensions;

namespace Inno.VisualNovel.Model.Story.Nodes.Extensions;

/// <summary>Invokes an explicitly described extension without embedding executable script text.</summary>
[GenerateSerializationConverter]
public sealed class CommandNode : ISerializable
{
    /// <summary>Gets or sets the preserved neutral implementation contract.</summary>
    [SerializableProperty] public NovelCommandDefinition definition { get; set; } = new();
    /// <summary>Gets or sets explicitly supplied arguments; omitted arguments use their declared defaults.</summary>
    [SerializableProperty] public CommandArgument[] arguments { get; set; } = [];
    /// <summary>Gets or sets a compatible result variable, empty to discard a result.</summary>
    [SerializableProperty] public string resultVariable { get; set; } = string.Empty;
}

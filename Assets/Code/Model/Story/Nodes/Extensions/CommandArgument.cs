using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Model.Story.Nodes.Extensions;

/// <summary>Supplies one command parameter through the shared typed expression model.</summary>
[GenerateSerializationConverter]
public sealed class CommandArgument : ISerializable
{
    /// <summary>Gets or sets the declared parameter identity.</summary>
    [SerializableProperty] public string id { get; set; } = string.Empty;
    /// <summary>Gets or sets the expression resolved once when entering the command.</summary>
    [SerializableProperty] public ExpressionDocument value { get; set; } = new();
}

using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Model.Extensions;

/// <summary>Declares a typed command parameter independently of a CLR field or editor widget.</summary>
[GenerateSerializationConverter]
public sealed class CommandParameter : ISerializable
{
    /// <summary>Gets or sets the stable argument identity.</summary>
    [SerializableProperty] public string id { get; set; } = string.Empty;
    /// <summary>Gets or sets its author-facing label.</summary>
    [SerializableProperty] public string label { get; set; } = string.Empty;
    /// <summary>Gets or sets its help text.</summary>
    [SerializableProperty] public string help { get; set; } = string.Empty;
    /// <summary>Gets or sets the typed default when an argument is omitted.</summary>
    [SerializableProperty] public NovelValue defaultValue { get; set; } = NovelValue.Boolean(false);
    /// <summary>Gets or sets permitted enum members, including enum-list members.</summary>
    [SerializableProperty] public string[] members { get; set; } = [];
    /// <summary>Validates a value using the same rules as story declarations.</summary>
    /// <param name="value">The candidate argument.</param>
    public void Validate(NovelValue value) => VariableValueRules.Validate(new() { id = id, name = label, scope = VariableScope.Local, initialValue = defaultValue, members = members }, value);
}

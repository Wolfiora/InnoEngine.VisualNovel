using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Model.Story.Nodes.Reading;

/// <summary>Defines one option with a stable graph output identity.</summary>
[GenerateSerializationConverter]
public sealed class ChoiceOption : ISerializable
{
    /// <summary>Gets or sets the stable option and output-port identity.</summary>
    [SerializableProperty]
    public string id { get; set; } = string.Empty;
    /// <summary>Gets or sets the displayed text.</summary>
    [SerializableProperty]
    public string text { get; set; } = string.Empty;
    /// <summary>Gets or sets the visibility condition.</summary>
    [SerializableProperty]
    public ExpressionDocument visible { get; set; } = new() { literal = NovelValue.Boolean(true) };
    /// <summary>Gets or sets the enabled condition.</summary>
    [SerializableProperty]
    public ExpressionDocument enabled { get; set; } = new() { literal = NovelValue.Boolean(true) };
    /// <summary>Gets or sets an explanation shown for disabled options.</summary>
    [SerializableProperty]
    public string disabledReason { get; set; } = string.Empty;
}

/// <summary>Defines one player decision and its timeout policy.</summary>
[GenerateSerializationConverter]
public sealed class ChoiceNode : ISerializable
{
    /// <summary>Gets or sets the prompt.</summary>
    [SerializableProperty]
    public string prompt { get; set; } = string.Empty;
    /// <summary>Gets or sets ordered stable options.</summary>
    [SerializableProperty]
    public ChoiceOption[] options { get; set; } = [];
    /// <summary>Gets or sets the timeout in seconds, zero for no timeout.</summary>
    [SerializableProperty]
    public double timeout { get; set; }
    /// <summary>Gets or sets the option chosen when a positive timeout expires.</summary>
    [SerializableProperty]
    public string defaultOption { get; set; } = string.Empty;
}

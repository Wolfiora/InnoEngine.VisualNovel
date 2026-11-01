using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Values;

/// <summary>Stores an explicit expression tree suitable for visual editing.</summary>
[GenerateSerializationConverter]
public sealed class ExpressionDocument : ISerializable
{
    /// <summary>Gets or sets the expression operator.</summary>
    [SerializableProperty]
    public ExpressionOperation operation { get; set; }
    /// <summary>Gets or sets a literal operand.</summary>
    [SerializableProperty]
    public NovelValue literal { get; set; } = NovelValue.Boolean(false);
    /// <summary>Gets or sets the stable variable identity.</summary>
    [SerializableProperty]
    public string variableId { get; set; } = string.Empty;
    /// <summary>Gets or sets ordered operands.</summary>
    [SerializableProperty]
    public ExpressionDocument[] operands { get; set; } = [];
}

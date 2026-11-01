using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Model.Collections;

/// <summary>Declares an explicit non-global initial value for a replay entry.</summary>
[GenerateSerializationConverter]
public sealed class CollectionVariable : ISerializable
{
    /// <summary>Gets or sets the declared variable identity.</summary>
    [SerializableProperty] public string id { get; set; } = string.Empty;
    /// <summary>Gets or sets its compatible initial value.</summary>
    [SerializableProperty] public NovelValue value { get; set; } = NovelValue.Boolean(false);
}

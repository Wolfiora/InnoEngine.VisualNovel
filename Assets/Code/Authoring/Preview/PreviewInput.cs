using InnoEngine.Serialization;

namespace Inno.VisualNovel.Authoring.Preview;

/// <summary>Supplies committed test input at an explicitly addressed interactive instruction.</summary>
[GenerateSerializationConverter]
public sealed class PreviewInput : ISerializable
{
    /// <summary>Gets or sets the authored chapter identity.</summary>
    [SerializableProperty] public string chapterId { get; set; } = string.Empty;
    /// <summary>Gets or sets the stable authored node identity.</summary>
    [SerializableProperty] public string nodeId { get; set; } = string.Empty;
    /// <summary>Gets or sets the literal committed text.</summary>
    [SerializableProperty] public string text { get; set; } = string.Empty;
    /// <summary>Gets or sets whether the recorded interaction cancels instead of submitting text.</summary>
    [SerializableProperty] public bool cancel { get; set; }
}

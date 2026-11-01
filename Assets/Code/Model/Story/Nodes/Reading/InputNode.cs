using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Story.Nodes.Reading;

/// <summary>Requests bounded Unicode player text and assigns it to a declared string variable.</summary>
[GenerateSerializationConverter]
public sealed class InputNode : ISerializable
{
    /// <summary>Gets or sets the displayed input prompt.</summary>
    [SerializableProperty] public string prompt { get; set; } = "请输入姓名";
    /// <summary>Gets or sets the declared destination string variable.</summary>
    [SerializableProperty] public string variableId { get; set; } = string.Empty;
    /// <summary>Gets or sets interpolated initial text.</summary>
    [SerializableProperty] public string defaultText { get; set; } = string.Empty;
    /// <summary>Gets or sets the minimum accepted grapheme count.</summary>
    [SerializableProperty] public int minimumLength { get; set; } = 1;
    /// <summary>Gets or sets the maximum accepted grapheme count.</summary>
    [SerializableProperty] public int maximumLength { get; set; } = 32;
    /// <summary>Gets or sets whether an explicit cancellation output is available.</summary>
    [SerializableProperty] public bool allowCancel { get; set; }
}

using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Story.Nodes.Debugging;

/// <summary>Specifies the three player-safe severities exposed by a visual story logging instruction.</summary>
public enum NovelLogLevel
{
    /// <summary>Records an informational observation.</summary>
    Information,
    /// <summary>Records a recoverable author warning.</summary>
    Warning,
    /// <summary>Records an author error without changing control flow; assertions stop execution separately.</summary>
    Error
}

/// <summary>Declares a source-qualified message written through the engine log router.</summary>
[GenerateSerializationConverter]
public sealed class LogNode : ISerializable
{
    /// <summary>Gets or sets a plain message using the ordinary story variable interpolation syntax.</summary>
    [SerializableProperty] public string message { get; set; } = "调试信息";
    /// <summary>Gets or sets the engine severity selected by the author.</summary>
    [SerializableProperty] public NovelLogLevel level { get; set; }
    /// <summary>Gets or sets whether published play suppresses this diagnostic while preserving its continuation.</summary>
    [SerializableProperty] public bool previewOnly { get; set; } = true;
}

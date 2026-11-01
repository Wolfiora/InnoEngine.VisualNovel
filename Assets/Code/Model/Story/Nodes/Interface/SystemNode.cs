using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Story.Nodes.Interface;

/// <summary>Declares a session-owned action without host objects, file paths or arbitrary script text.</summary>
[GenerateSerializationConverter]
public sealed class SystemNode : ISerializable
{
    /// <summary>Gets or sets the concrete implemented action.</summary>
    [SerializableProperty] public SystemOperation operation { get; set; }
    /// <summary>Gets or sets a page role or theme resource identity; ReturnTitle has no argument.</summary>
    [SerializableProperty] public string argument { get; set; } = "settings";
}

using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Extensions;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Model.Interface.Actions;

/// <summary>Describes one synchronous UI action and its typed neutral argument independently of implementation instances.</summary>
[GenerateSerializationConverter]
public sealed class UiActionDefinition : ISerializable
{
    /// <summary>Gets or sets a globally unique custom action identity beginning with custom.</summary>
    [SerializableProperty] public string id { get; set; } = string.Empty;
    /// <summary>Gets or sets the author-facing action name.</summary>
    [SerializableProperty] public string title { get; set; } = string.Empty;
    /// <summary>Gets or sets contextual authoring help.</summary>
    [SerializableProperty] public string help { get; set; } = string.Empty;
    /// <summary>Gets or sets the same typed argument schema used by command authoring.</summary>
    [SerializableProperty] public CommandParameter argument { get; set; } = new() { id = "argument", label = "参数", defaultValue = NovelValue.String("") };
    /// <summary>Gets or sets preview, native-state and rollback guarantees; asynchronous cancellation is not part of this instant action contract.</summary>
    [SerializableProperty] public CommandCapabilities capabilities { get; set; } = CommandCapabilities.Preview | CommandCapabilities.Save | CommandCapabilities.Rollback;
}

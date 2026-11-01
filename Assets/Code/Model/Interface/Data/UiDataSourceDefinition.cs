using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Extensions;

namespace Inno.VisualNovel.Model.Interface.Data;

/// <summary>Describes an explicitly registered, session-derived source without serializing its implementation.</summary>
[GenerateSerializationConverter]
public sealed class UiDataSourceDefinition : ISerializable
{
    /// <summary>Gets or sets the stable single-segment identity addressed as custom.id.field.</summary>
    [SerializableProperty] public string id { get; set; } = string.Empty;
    /// <summary>Gets or sets the author-facing source name.</summary>
    [SerializableProperty] public string title { get; set; } = string.Empty;
    /// <summary>Gets or sets help describing state ownership and binding usage.</summary>
    [SerializableProperty] public string help { get; set; } = string.Empty;
    /// <summary>Gets or sets root value declarations.</summary>
    [SerializableProperty] public UiDataFieldDefinition[] fields { get; set; } = [];
    /// <summary>Gets or sets stable keyed collection declarations.</summary>
    [SerializableProperty] public UiDataCollectionDefinition[] collections { get; set; } = [];
    /// <summary>Gets or sets local image binding names resolved through the ordinary UiDataSource image method.</summary>
    [SerializableProperty] public string[] images { get; set; } = [];
    /// <summary>Gets or sets preview and native-state guarantees, including write rollback policy.</summary>
    [SerializableProperty] public CommandCapabilities capabilities { get; set; } = CommandCapabilities.Preview | CommandCapabilities.Save | CommandCapabilities.Rollback;
}

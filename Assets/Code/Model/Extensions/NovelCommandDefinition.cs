using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Model.Extensions;

/// <summary>Describes a stable command contract used by authoring and generation-owned runtime registration.</summary>
[GenerateSerializationConverter]
public sealed class NovelCommandDefinition : ISerializable
{
    /// <summary>Gets or sets the stable implementation identity.</summary>
    [SerializableProperty] public string id { get; set; } = string.Empty;
    /// <summary>Gets or sets its display name.</summary>
    [SerializableProperty] public string title { get; set; } = string.Empty;
    /// <summary>Gets or sets its author-facing help.</summary>
    [SerializableProperty] public string help { get; set; } = string.Empty;
    /// <summary>Gets or sets explicitly supported lifecycle guarantees.</summary>
    [SerializableProperty] public CommandCapabilities capabilities { get; set; }
    /// <summary>Gets or sets ordered typed parameters.</summary>
    [SerializableProperty] public CommandParameter[] parameters { get; set; } = [];
    /// <summary>Gets or sets whether successful execution must return a value.</summary>
    [SerializableProperty] public bool hasResult { get; set; }
    /// <summary>Gets or sets the declared result type and default shape.</summary>
    [SerializableProperty] public NovelValue resultDefault { get; set; } = NovelValue.Boolean(false);
    /// <summary>Gets or sets permitted result enum members.</summary>
    [SerializableProperty] public string[] resultMembers { get; set; } = [];
}

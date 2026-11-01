using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Values;

/// <summary>Identifies the lifetime of a declared story variable.</summary>
public enum VariableScope
{
    /// <summary>Belongs to its active local owner, such as a story invocation or UI page visit.</summary>
    Local,
    /// <summary>Belongs to one saved game.</summary>
    Game,
    /// <summary>Survives ordinary loads and rollback.</summary>
    Global
}

/// <summary>Declares one typed variable independently of display names.</summary>
[GenerateSerializationConverter]
public sealed class VariableDefinition : ISerializable
{
    /// <summary>Gets or sets the stable variable identity.</summary>
    [SerializableProperty]
    public string id { get; set; } = string.Empty;
    /// <summary>Gets or sets the author-facing label.</summary>
    [SerializableProperty]
    public string name { get; set; } = string.Empty;
    /// <summary>Gets or sets an author-facing group; it never changes identity or scope.</summary>
    [SerializableProperty] public string group { get; set; } = string.Empty;
    /// <summary>Gets or sets author documentation about the value and its intended use.</summary>
    [SerializableProperty] public string description { get; set; } = string.Empty;
    /// <summary>Gets or sets the persistence lifetime.</summary>
    [SerializableProperty]
    public VariableScope scope { get; set; } = VariableScope.Game;
    /// <summary>Gets or sets the typed initial value.</summary>
    [SerializableProperty]
    public NovelValue initialValue { get; set; } = NovelValue.Boolean(false);
    /// <summary>Gets or sets permitted symbolic members for Enum variables.</summary>
    [SerializableProperty]
    public string[] members { get; set; } = [];
}

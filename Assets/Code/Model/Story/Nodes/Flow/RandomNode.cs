using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Story.Nodes.Flow;

/// <summary>Defines a positive weight on a stable output port.</summary>
[GenerateSerializationConverter]
public sealed class WeightedBranch : ISerializable
{
    /// <summary>Gets or sets the stable output identity.</summary>
    [SerializableProperty]
    public string id { get; set; } = string.Empty;
    /// <summary>Gets or sets the positive relative weight.</summary>
    [SerializableProperty]
    public double weight { get; set; } = 1;
}

/// <summary>Chooses a branch from the session-owned deterministic random state.</summary>
[GenerateSerializationConverter]
public sealed class RandomNode : ISerializable
{
    /// <summary>Gets or sets ordered weighted branches.</summary>
    [SerializableProperty]
    public WeightedBranch[] branches { get; set; } = [];
}

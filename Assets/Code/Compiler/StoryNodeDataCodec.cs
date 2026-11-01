using System;
using InnoEngine.Graphs;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Compiler;

/// <summary>Encodes node-owned typed settings through the editor's native serializer.</summary>
public sealed class StoryNodeDataCodec
{
    private readonly IStorySerialization m_serialization;

    /// <summary>Creates an operation owner for reference-free node settings.</summary>
    /// <param name="serialization">The active editor serialization registry.</param>
    public StoryNodeDataCodec(IStorySerialization serialization) => m_serialization = serialization ?? throw new ArgumentNullException(nameof(serialization));

    /// <summary>Writes one complete typed node payload without changing node identity or connections.</summary>
    /// <typeparam name="T">A closed settings contract containing values only.</typeparam>
    /// <param name="node">The destination node.</param>
    /// <param name="value">The settings copied into neutral native bytes.</param>
    public void Write<T>(
        GraphNodeRecord node,
        T value
    ) where T : class, ISerializable
        => node.SetValue("settings", new GraphSerializedValue(m_serialization.Serialize(value)));

    /// <summary>Reads a detached typed settings value.</summary>
    /// <typeparam name="T">The expected closed settings contract.</typeparam>
    /// <param name="node">The source graph node.</param>
    /// <returns>A newly restored settings object.</returns>
    /// <exception cref="InvalidOperationException">The node does not contain settings.</exception>
    public T Read<T>(GraphNodeRecord node) where T : class, ISerializable
        => node.TryGetValue("settings", out GraphSerializedValue? data) && data is not null
            ? m_serialization.Deserialize<T>(data.data.Span)
            : throw new InvalidOperationException($"Node '{node.id}' has no settings.");
}

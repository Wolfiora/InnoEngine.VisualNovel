using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Graphs;
using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Compiler.Extensions;

/// <summary>Provides detached native node values to a pure, source-addressable extension lowering operation.</summary>
public sealed class StoryNodeCompilationContext
{
    private readonly StoryNodeDataCodec m_codec;
    private readonly GraphNodeRecord m_node;
    private readonly VariableDefinition[] m_variables;
    internal StoryNodeCompilationContext(
        string chapterId,
        GraphNodeRecord node,
        StoryNodeDataCodec codec,
        IEnumerable<VariableDefinition> variables
    ) { this.chapterId = chapterId; m_node = node; m_codec = codec; m_variables = variables.Select(VariableValueRules.Copy).ToArray(); }
    /// <summary>Gets the original chapter identity retained by diagnostics, runtime tracing and save restoration.</summary>
    public string chapterId { get; }
    /// <summary>Gets the stable authored node identity; lowering cannot allocate another execution cursor.</summary>
    public string nodeId => m_node.id.value;
    /// <summary>Gets independent typed declarations including the explicitly associated shared variable library.</summary>
    public IReadOnlyList<VariableDefinition> variables => m_variables.Select(VariableValueRules.Copy).ToArray();
    /// <summary>Restores an independent custom settings value using the engine's current native serializer.</summary>
    /// <typeparam name="T">The reference-free serialized settings contract.</typeparam>
    /// <returns>Detached settings that cannot modify the authoritative graph.</returns>
    public T Read<T>() where T : class, ISerializable => m_codec.Read<T>(m_node);
    /// <summary>Creates an independent built-in instruction carrying the original cursor and layout position.</summary>
    /// <typeparam name="T">The chosen built-in settings contract.</typeparam>
    /// <param name="definitionId">The built-in operation to validate and execute.</param>
    /// <param name="settings">Settings copied into native bytes immediately.</param>
    /// <returns>A detached graph record; its output contract must match the authored extension ports.</returns>
    public GraphNodeRecord Create<T>(
        string definitionId,
        T settings
    ) where T : class, ISerializable
    {
        ArgumentNullException.ThrowIfNull(settings);
        var result = new GraphNodeRecord(m_node.id, definitionId) { position = m_node.position };
        m_codec.Write(result, settings); return result;
    }
}

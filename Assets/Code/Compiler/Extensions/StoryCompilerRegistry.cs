using System;
using System.Collections.Generic;

namespace Inno.VisualNovel.Compiler.Extensions;

/// <summary>Owns explicit compiler factories for one editor generation without global registration or assembly scanning.</summary>
public sealed class StoryCompilerRegistry
{
    private readonly Dictionary<string, Func<StoryNodeCompiler>> m_factories = new(StringComparer.Ordinal);
    /// <summary>Registers a pure factory and rejects reserved or duplicate identities before compilation.</summary>
    /// <param name="factory">Creates an independent definition for each compilation operation.</param>
    public void Register(Func<StoryNodeCompiler> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        StoryNodeCompiler definition = factory() ?? throw new InvalidOperationException("The node compiler factory returned no definition.");
        if (definition.id.StartsWith("inno.vn.", StringComparison.Ordinal)) throw new InvalidOperationException("The inno.vn namespace is reserved for built-in instructions.");
        if (!m_factories.TryAdd(definition.id, factory)) throw new InvalidOperationException("Node compiler identity is already registered: " + definition.id);
    }
    /// <summary>Releases generation-owned factories; existing programs contain only lowered neutral data.</summary>
    public void Clear() => m_factories.Clear();
    internal IReadOnlyDictionary<string, StoryNodeCompiler> Create()
    {
        var result = new Dictionary<string, StoryNodeCompiler>(StringComparer.Ordinal);
        foreach (var pair in m_factories)
        {
            var definition = pair.Value() ?? throw new InvalidOperationException("The node compiler factory returned no definition: " + pair.Key);
            if (definition.id != pair.Key) throw new InvalidOperationException("The node compiler factory changed its registered identity: " + pair.Key);
            result.Add(pair.Key, definition);
        }
        return result;
    }
}

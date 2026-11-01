using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Narrative.Values;

/// <summary>Stores one persistent variable value in a neutral checkpoint.</summary>
[GenerateSerializationConverter]
public sealed class VariableState : ISerializable
{
    /// <summary>Gets or sets the variable semantic identity.</summary>
    [SerializableProperty]
    public string id { get; set; } = string.Empty;
    /// <summary>Gets or sets a detached typed value.</summary>
    [SerializableProperty]
    public NovelValue value { get; set; } = NovelValue.Boolean(false);
}

/// <summary>Owns typed variables for one lifetime and validates every write and restoration.</summary>
public sealed class VariableStore
{
    private readonly Dictionary<string, VariableDefinition> m_definitions;
    private Dictionary<string, NovelValue> m_values;
    /// <summary>Gets an owner-local counter changed by committed writes and restorations.</summary>
    public long revision { get; private set; }

    /// <summary>Creates a store from declarations and independent default values.</summary>
    /// <param name="definitions">The declarations owned by this scope.</param>
    public VariableStore(IEnumerable<VariableDefinition> definitions)
    {
        m_definitions = definitions.ToDictionary(static item => item.id, VariableValueRules.Copy, StringComparer.Ordinal);
        m_values = m_definitions.ToDictionary(static item => item.Key, static item => item.Value.initialValue.Copy(), StringComparer.Ordinal);
        foreach ((string id, NovelValue value) in m_values)
            Validate(id, value);
    }

    /// <summary>Gets whether a stable variable is owned by this scope.</summary>
    /// <param name="id">The variable identity.</param>
    /// <returns>True when declared.</returns>
    public bool Contains(string id) => m_definitions.ContainsKey(id);

    /// <summary>Reads a defensive value copy.</summary>
    /// <param name="id">The declared variable identity.</param>
    /// <returns>A detached value; unknown identities throw.</returns>
    public NovelValue Get(string id) => m_values.TryGetValue(id, out NovelValue? value)
        ? value.Copy() : throw new InvalidOperationException($"Unknown variable '{id}'.");

    /// <summary>Validates and assigns a detached value.</summary>
    /// <param name="id">The declared variable identity.</param>
    /// <param name="value">The complete compatible value.</param>
    public void Set(
        string id,
        NovelValue value
    ) {
        ArgumentNullException.ThrowIfNull(value);
        Validate(id, value);
        if (!m_values[id].Equals(value)) { m_values[id] = value.Copy(); revision++; }
    }

    /// <summary>Captures values in deterministic identity order.</summary>
    /// <returns>A snapshot sharing no mutable value storage.</returns>
    public VariableState[] Capture() => m_values.OrderBy(static item => item.Key, StringComparer.Ordinal)
        .Select(static item => new VariableState { id = item.Key, value = item.Value.Copy() }).ToArray();

    /// <summary>Atomically restores an exact declaration-matching snapshot.</summary>
    /// <param name="snapshot">All values of this scope.</param>
    public void Restore(VariableState[] snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Length != m_definitions.Count)
            throw new InvalidOperationException("Variable snapshot does not match its declarations.");
        var candidate = new Dictionary<string, NovelValue>(StringComparer.Ordinal);
        foreach (VariableState item in snapshot)
        {
            Validate(item.id, item.value);
            if (!candidate.TryAdd(item.id, item.value.Copy()))
                throw new InvalidOperationException($"Duplicate restored variable '{item.id}'.");
        }
        m_values = candidate;
        revision++;
    }

    private void Validate(
        string id,
        NovelValue value
    ) {
        if (!m_definitions.TryGetValue(id, out VariableDefinition? definition))
            throw new InvalidOperationException($"Unknown variable '{id}'.");
        VariableValueRules.Validate(definition, value);
    }
}

using System;
using System.Linq;
using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Narrative.Values;

namespace Inno.VisualNovel.Narrative.Extensions;

/// <summary>Stores bounded neutral command data without tasks, native handles or implementation objects.</summary>
[GenerateSerializationConverter]
public sealed class NovelCommandState : ISerializable
{
    private VariableState[] m_values = [];
    /// <summary>Gets or sets elapsed logical seconds owned by the command scheduler.</summary>
    [SerializableProperty] public double elapsed { get; set; }
    /// <summary>Gets or sets whether at least one execution step has completed.</summary>
    [SerializableProperty] public bool started { get; set; }
    /// <summary>Gets or sets detached keyed values used by native serialization and restoration.</summary>
    [SerializableProperty] public VariableState[] values
    {
        get => m_values.Select(static item => new VariableState { id = item.id, value = item.value.Copy() }).ToArray();
        set
        {
            if (value.Length > 256 || value.Any(static item => string.IsNullOrWhiteSpace(item.id)) || value.Select(static item => item.id).Distinct(StringComparer.Ordinal).Count() != value.Length)
                throw new InvalidOperationException("Command state requires at most 256 uniquely identified values.");
            foreach (var item in value) ValidateValue(item.value);
            m_values = value.Select(static item => new VariableState { id = item.id, value = item.value.Copy() }).ToArray();
        }
    }
    /// <summary>Reads a defensive value copy or a supplied typed default.</summary>
    /// <param name="id">The command-local data key.</param>
    /// <param name="fallback">The value used before the command first writes the key.</param>
    /// <returns>A detached value.</returns>
    public NovelValue Get(
        string id,
        NovelValue fallback
    ) => (m_values.FirstOrDefault(item => item.id == id)?.value ?? fallback).Copy();
    /// <summary>Writes a detached bounded value without modifying story variables.</summary>
    /// <param name="id">The command-local key.</param>
    /// <param name="value">The new neutral value.</param>
    public void Set(
        string id,
        NovelValue value
    ) => values = [.. m_values.Where(item => item.id != id), new() { id = id, value = value }];
    /// <summary>Copies and validates the complete command-local payload.</summary>
    /// <returns>A detached snapshot.</returns>
    public NovelCommandState Copy()
    {
        if (!double.IsFinite(elapsed) || elapsed < 0) throw new InvalidOperationException("Command logical time is invalid.");
        return new() { values = values, elapsed = elapsed, started = started };
    }
    private static void ValidateValue(NovelValue value)
    {
        if (value.kind is NovelValueKind.String or NovelValueKind.Enum && value.text.Length > 1048576 || value.kind == NovelValueKind.List && value.items.Length > 4096)
            throw new InvalidOperationException("Command data exceeds its string or list budget.");
        if (value.kind == NovelValueKind.List) foreach (var item in value.items) ValidateValue(item);
    }
}

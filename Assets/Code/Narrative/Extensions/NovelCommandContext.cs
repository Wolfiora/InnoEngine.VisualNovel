using System;
using System.Collections.Generic;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Narrative.Extensions;

/// <summary>Allows scoped argument reads, defensive story observation and one typed result on the owner thread.</summary>
public sealed class NovelCommandContext
{
    private readonly Func<string, NovelValue> m_variables;
    private readonly IReadOnlyDictionary<string, NovelValue> m_arguments;
    private readonly int m_thread = Environment.CurrentManagedThreadId;
    private bool m_open;
    internal NovelValue? result { get; private set; }
    internal NovelCommandContext(
        Func<string, NovelValue> variables,
        IReadOnlyDictionary<string, NovelValue> arguments
    ) { m_variables = variables; m_arguments = arguments; }
    /// <summary>Reads one resolved typed argument.</summary>
    /// <param name="id">A declared parameter identity.</param>
    /// <returns>A detached value.</returns>
    public NovelValue Argument(string id)
    {
        Check(); return m_arguments.TryGetValue(id, out var value) ? value.Copy() : throw new InvalidOperationException("Command argument is unavailable: " + id);
    }
    /// <summary>Observes a declared variable without granting an unrestricted write channel.</summary>
    /// <param name="id">The declared variable identity.</param>
    /// <returns>A detached typed value.</returns>
    public NovelValue ReadVariable(string id) { Check(); return m_variables(id); }
    /// <summary>Supplies a result validated against the command schema before writing its explicit destination variable.</summary>
    /// <param name="value">The complete neutral result.</param>
    public void Return(NovelValue value) { Check(); result = value.Copy(); }
    internal void Enter() { CheckThread(); m_open = true; result = null; }
    internal void Leave() => m_open = false;
    private void Check() { CheckThread(); if (!m_open) throw new InvalidOperationException("Command context is no longer active; queue external results and consume them in Execute."); }
    private void CheckThread() { if (Environment.CurrentManagedThreadId != m_thread) throw new InvalidOperationException("Command context must be used on its session owner thread."); }
}

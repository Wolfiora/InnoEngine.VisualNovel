using System;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Narrative.Debugging;

/// <summary>Owns a stable source breakpoint and a pure independently copied Boolean condition.</summary>
public sealed class StoryBreakpoint
{
    private readonly ExpressionDocument? m_condition;
    /// <summary>Creates a source breakpoint without retaining caller-owned expression data.</summary>
    /// <param name="chapter">The authored chapter identity.</param>
    /// <param name="node">The authored graph node identity.</param>
    /// <param name="condition">An optional pure Boolean expression evaluated in the stopped task's scope.</param>
    public StoryBreakpoint(
        string chapter,
        string node,
        ExpressionDocument? condition = null
    ) {
        ArgumentException.ThrowIfNullOrWhiteSpace(chapter); ArgumentException.ThrowIfNullOrWhiteSpace(node);
        this.chapter = chapter; this.node = node;
        m_condition = condition is null ? null : ExpressionOperations.Copy(condition);
        if (m_condition is not null && ExpressionOperations.UsesRandom(m_condition)) throw new ArgumentException("A breakpoint cannot consume narrative random state.", nameof(condition));
    }
    /// <summary>Gets the authored chapter identity.</summary>
    public string chapter { get; }
    /// <summary>Gets the authored node identity.</summary>
    public string node { get; }
    /// <summary>Gets whether this breakpoint evaluates a condition, without copying the owned expression tree.</summary>
    public bool conditional => m_condition is not null;
    /// <summary>Gets an independent condition copy for editing, or null for an unconditional breakpoint.</summary>
    public ExpressionDocument? condition => m_condition is null ? null : ExpressionOperations.Copy(m_condition);
    internal bool Matches(Func<string, NovelValue> read) => m_condition is null || new Values.ExpressionEvaluator(read).Evaluate(m_condition).boolean;
}

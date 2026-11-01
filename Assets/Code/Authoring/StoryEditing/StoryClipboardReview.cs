using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Authoring.StoryEditing;

/// <summary>Describes a neutral copied selection and its external destinations without exposing mutable clipboard graph data.</summary>
public sealed class StoryClipboardReview
{
    private readonly VariableDefinition[] m_variables;
    internal StoryClipboardReview(StoryClipboardState state)
    {
        chapterId = state.chapterId;
        m_variables = state.variables.Select(VariableValueRules.Copy).ToArray();
        var nodes = state.graph.document.nodes.Select(static node => node.id.value).ToHashSet(StringComparer.Ordinal);
        nodeIds = Array.AsReadOnly(state.graph.document.nodes.Select(static node => node.id.value).ToArray());
        externalTargets = Array.AsReadOnly(state.graph.document.edges.Where(edge => !nodes.Contains(edge.input.nodeId.value)).Select(static edge => edge.input.nodeId.value)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }
    /// <summary>Gets the original chapter identity, allowing valid same-chapter references to remain explicit.</summary>
    public string chapterId { get; }
    /// <summary>Gets original selected node identities in authored order.</summary>
    public IReadOnlyList<string> nodeIds { get; }
    /// <summary>Gets unique outgoing destinations outside the copied group, requiring retention, mapping or Missing preservation.</summary>
    public IReadOnlyList<string> externalTargets { get; }
    /// <summary>Copies source declarations for explicit conflict review without exposing the captured clipboard's mutable defaults.</summary>
    /// <returns>Independent declarations including scope, enum membership and typed initial values.</returns>
    public VariableDefinition[] CopyVariables() => m_variables.Select(VariableValueRules.Copy).ToArray();
}

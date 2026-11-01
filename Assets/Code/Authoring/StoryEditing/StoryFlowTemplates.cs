using System;
using InnoEngine.Graphs;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Model.Story.Nodes.Variables;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Authoring.StoryEditing;

/// <summary>Builds visual flow templates as ordinary editable graph topology, never a parallel control-flow model.</summary>
public sealed class StoryFlowTemplates
{
    private readonly StoryEditService m_edits;
    /// <summary>Creates a template builder using the same graph semantic operations as both editor views.</summary>
    /// <param name="codec">The native node settings owner.</param>
    public StoryFlowTemplates(StoryNodeDataCodec codec) => m_edits = new(codec);
    /// <summary>Creates a counted loop with a local counter and an editable interactive body.</summary>
    /// <param name="draft">The detached chapter candidate.</param>
    /// <param name="fragmentId">The owning fragment.</param>
    /// <param name="count">The nonnegative iteration count.</param>
    /// <param name="position">The finite graph origin.</param>
    /// <returns>Ordinary entry, body and exit node identities for explicit connection.</returns>
    public StoryLoopTemplate AddCountedLoop(
        StoryDraftState draft,
        string fragmentId,
        long count,
        GraphPosition position
    ) {
        if (count < 0 || count > 1000000 || !float.IsFinite(position.x) || !float.IsFinite(position.y)) throw new ArgumentException("The counted loop needs a finite origin and a count between zero and one million.");
        string variable = "loop/" + Guid.NewGuid().ToString("N");
        draft.variables = [.. draft.variables, new() { id = variable, name = "循环计数", scope = VariableScope.Local, initialValue = NovelValue.Integer(0) }];
        GraphNodeId initialize = m_edits.Add(draft, fragmentId, StoryNodeIds.assign, new AssignmentNode { variableId = variable, value = new() { literal = NovelValue.Integer(0) } }, position);
        GraphNodeId condition = m_edits.Add(draft, fragmentId, StoryNodeIds.condition, new ConditionNode { condition = new() { operation = ExpressionOperation.Less,
            operands = [new() { operation = ExpressionOperation.Variable, variableId = variable }, new() { literal = NovelValue.Integer(count) }] } }, new(position.x + 260, position.y));
        GraphNodeId body = m_edits.Add(draft, fragmentId, StoryNodeIds.dialogue, new DialogueNode { textId = Guid.NewGuid().ToString("N"), text = "循环正文，当前计数：{" + variable + "}" }, new(position.x + 520, position.y));
        GraphNodeId increment = m_edits.Add(draft, fragmentId, StoryNodeIds.assign, new AssignmentNode { variableId = variable, value = new() { operation = ExpressionOperation.Add,
            operands = [new() { operation = ExpressionOperation.Variable, variableId = variable }, new() { literal = NovelValue.Integer(1) }] } }, new(position.x + 520, position.y + 200));
        GraphNodeId exit = m_edits.Add(draft, fragmentId, StoryNodeIds.wait, new WaitNode { seconds = 0 }, new(position.x + 260, position.y + 400));
        m_edits.Connect(draft, new(initialize, new("next")), condition);
        m_edits.Connect(draft, new(condition, new("true")), body);
        m_edits.Connect(draft, new(condition, new("false")), exit);
        m_edits.Connect(draft, new(body, new("next")), increment);
        m_edits.Connect(draft, new(increment, new("next")), condition);
        return new(initialize.value, body.value, exit.value, variable);
    }
}

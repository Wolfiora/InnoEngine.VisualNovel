using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Graphs;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Model.Story.Nodes.Variables;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Authoring.StoryEditing;

/// <summary>Owns variable declaration validation and reference-aware removal.</summary>
public sealed class VariableEditService(StoryNodeDataCodec codec)
{
    /// <summary>Adds a declaration with a stable, author-assigned identity.</summary>
    /// <param name="draft">The candidate.</param>
    /// <param name="definition">The declaration to copy.</param>
    public void Add(
        StoryDraftState draft,
        VariableDefinition definition
    ) {
        Validate(definition);
        if (draft.GetVariables().Any(item => item.id == definition.id)) throw new InvalidOperationException("The variable identity already exists in this chapter or its shared library.");
        draft.variables = [.. draft.variables, VariableValueRules.Copy(definition)];
    }

    /// <summary>Replaces editable declaration data while keeping its stable identity.</summary>
    /// <param name="draft">The candidate.</param>
    /// <param name="definition">The complete replacement declaration.</param>
    public void Update(
        StoryDraftState draft,
        VariableDefinition definition
    ) {
        Validate(definition);
        int index = Array.FindIndex(draft.variables, item => item.id == definition.id);
        if (index < 0) throw new ArgumentException("Variable is missing.", nameof(definition));
        draft.variables[index] = VariableValueRules.Copy(definition);
    }

    /// <summary>Removes an unreferenced declaration; references must be changed explicitly first.</summary>
    /// <param name="draft">The candidate.</param>
    /// <param name="id">The stable variable identity.</param>
    public void Remove(
        StoryDraftState draft,
        string id
    ) {
        if (FindUsages(draft, id).Count > 0) throw new InvalidOperationException("Change the displayed node references before removing this variable.");
        if (!draft.variables.Any(item => item.id == id)) throw new ArgumentException("Variable is missing.", nameof(id));
        draft.variables = draft.variables.Where(item => item.id != id).ToArray();
    }

    /// <summary>Finds settings references and conservatively retains declarations when a node cannot be inspected.</summary>
    /// <param name="draft">The authored state.</param>
    /// <param name="id">The variable identity.</param>
    /// <returns>Stable node identities, suitable for navigation.</returns>
    public IReadOnlyList<string> FindUsages(
        StoryDraftState draft,
        string id
    ) {
        var usages = new List<string>();
        foreach (GraphNodeRecord node in draft.graph.document.nodes)
        {
            bool used;
            try
            {
                used = node.definitionId switch
                {
                    StoryNodeIds.command => codec.Read<Model.Story.Nodes.Extensions.CommandNode>(node).resultVariable == id || codec.Read<Model.Story.Nodes.Extensions.CommandNode>(node).arguments.Any(argument => ExpressionUses(argument.value, id)),
                    StoryNodeIds.dialogue => ContainsInterpolation(codec.Read<DialogueNode>(node).text, id) || ContainsInterpolation(codec.Read<DialogueNode>(node).speaker, id),
                    StoryNodeIds.log => ContainsInterpolation(codec.Read<Model.Story.Nodes.Debugging.LogNode>(node).message, id),
                    StoryNodeIds.input => codec.Read<InputNode>(node).variableId == id || ContainsInterpolation(codec.Read<InputNode>(node).prompt, id) || ContainsInterpolation(codec.Read<InputNode>(node).defaultText, id),
                    StoryNodeIds.assign => AssignmentUses(codec.Read<AssignmentNode>(node)),
                    StoryNodeIds.condition or StoryNodeIds.assertion => ExpressionUses(codec.Read<ConditionNode>(node).condition, id),
                    StoryNodeIds.choice => ChoiceUses(codec.Read<ChoiceNode>(node)),
                    StoryNodeIds.entry or StoryNodeIds.end or StoryNodeIds.returnFromCall or StoryNodeIds.wait or StoryNodeIds.call or StoryNodeIds.random or StoryNodeIds.stage or StoryNodeIds.audio or StoryNodeIds.performance or StoryNodeIds.unlock or StoryNodeIds.parallel or StoryNodeIds.join or StoryNodeIds.screenEffect or StoryNodeIds.transition or StoryNodeIds.system => false,
                    _ => true
                };
            }
            catch (Exception failure) when (failure is not OutOfMemoryException) { used = true; }
            if (used) usages.Add(node.id.value);
        }
        return usages;

        bool AssignmentUses(AssignmentNode assignment) => assignment.variableId == id || ExpressionUses(assignment.value, id);
        bool ChoiceUses(ChoiceNode choice) => ContainsInterpolation(choice.prompt, id) || choice.options.Any(option => ContainsInterpolation(option.text, id)
            || ContainsInterpolation(option.disabledReason, id) || ExpressionUses(option.visible, id) || ExpressionUses(option.enabled, id));
    }

    private static bool ContainsInterpolation(
        string text,
        string id
    ) => Model.Text.NovelTextTemplate.Parse(text).Any(token => token.variable && token.value == id);

    private static bool ExpressionUses(
        ExpressionDocument expression,
        string id,
        int depth = 0
    ) => depth >= 64 || expression.operation == ExpressionOperation.Variable && expression.variableId == id
        || expression.operands.Any(item => ExpressionUses(item, id, depth + 1));

    private static void Validate(VariableDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.id);
        if (definition.id.Contains('{') || definition.id.Contains('}') || definition.initialValue is null || !Enum.IsDefined(definition.scope))
            throw new ArgumentException("Variable identity, scope or default value is invalid.", nameof(definition));
        VariableValueRules.Validate(definition, definition.initialValue);
    }

}

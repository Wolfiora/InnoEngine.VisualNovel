using System;
using System.Linq;
using InnoEngine.Graphs;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Model.Story.Nodes.Variables;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Model.Story.Nodes.Audio;
using Inno.VisualNovel.Model.Story.Nodes.Extensions;

namespace Inno.VisualNovel.Authoring.StoryEditing;

/// <summary>Creates usable built-in defaults independently of either editor presentation.</summary>
public sealed class StoryNodeFactory(StoryEditService edits)
{
    /// <summary>Creates one node; connecting it remains an explicit document operation.</summary>
    /// <param name="draft">The candidate document.</param>
    /// <param name="fragmentId">The owning fragment.</param>
    /// <param name="definitionId">One supported built-in identity.</param>
    /// <param name="position">The layout position.</param>
    /// <returns>The newly assigned stable identity.</returns>
    public GraphNodeId Create(
        StoryDraftState draft,
        string fragmentId,
        string definitionId,
        GraphPosition position
    ) {
        VariableDefinition[] variables = draft.GetVariables();
        return definitionId switch
        {
            StoryNodeIds.log => edits.Add(draft, fragmentId, definitionId, new Model.Story.Nodes.Debugging.LogNode(), position),
            StoryNodeIds.parallel => edits.AddParallel(draft, fragmentId, position),
            StoryNodeIds.join => edits.Add(draft, fragmentId, definitionId, new JoinNode(), position),
            StoryNodeIds.command => edits.Add(draft, fragmentId, definitionId, new CommandNode(), position),
            StoryNodeIds.unlock => edits.Add(draft, fragmentId, definitionId, new UnlockNode(), position),
            StoryNodeIds.system => edits.Add(draft, fragmentId, definitionId, new Model.Story.Nodes.Interface.SystemNode(), position),
            StoryNodeIds.stage => edits.Add(draft, fragmentId, definitionId, new StageNode { operation = StageOperation.SetCamera }, position),
            StoryNodeIds.screenEffect => edits.Add(draft, fragmentId, definitionId, new ScreenEffectNode(), position),
            StoryNodeIds.transition => edits.Add(draft, fragmentId, definitionId, new TransitionNode(), position),
            StoryNodeIds.audio => edits.Add(draft, fragmentId, definitionId, new AudioNode(), position),
            StoryNodeIds.performance => edits.Add(draft, fragmentId, definitionId, new PerformanceNode(), position),
            StoryNodeIds.entry or StoryNodeIds.end or StoryNodeIds.returnFromCall => edits.Add(draft, fragmentId, definitionId, position),
            StoryNodeIds.dialogue => edits.Add(draft, fragmentId, definitionId, new DialogueNode { textId = Guid.NewGuid().ToString("N"), text = "新的对白" }, position),
            StoryNodeIds.input => edits.Add(draft, fragmentId, definitionId, new InputNode { variableId = variables.FirstOrDefault(static item => item.initialValue.kind == NovelValueKind.String)?.id ?? string.Empty }, position),
            StoryNodeIds.choice => edits.Add(draft, fragmentId, definitionId, new ChoiceNode
            {
                options = [new() { id = "option1", text = "选项一" }, new() { id = "option2", text = "选项二" }]
            }, position),
            StoryNodeIds.wait => edits.Add(draft, fragmentId, definitionId, new WaitNode(), position),
            StoryNodeIds.call => edits.Add(draft, fragmentId, definitionId, new CallNode { entryId = draft.entries.Length > 0 ? draft.entries[0].id : "start" }, position),
            StoryNodeIds.condition or StoryNodeIds.assertion => edits.Add(draft, fragmentId, definitionId, new ConditionNode { condition = new() { literal = NovelValue.Boolean(true) } }, position),
            StoryNodeIds.random => edits.Add(draft, fragmentId, definitionId, new RandomNode { branches = [new() { id = "branch1" }, new() { id = "branch2" }] }, position),
            StoryNodeIds.assign => edits.Add(draft, fragmentId, definitionId, new AssignmentNode
            {
                variableId = variables.Length > 0 ? variables[0].id : string.Empty,
                value = new() { literal = variables.Length > 0 ? variables[0].initialValue.Copy() : NovelValue.Boolean(false) }
            }, position),
            _ => throw new ArgumentException("The built-in node implementation is unavailable.", nameof(definitionId))
        };
    }
}

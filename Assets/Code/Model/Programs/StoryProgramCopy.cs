using System;
using System.Linq;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Model.Story.Nodes.Variables;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Model.Story.Nodes.Audio;
using Inno.VisualNovel.Model.Story.Nodes.Interface;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Model.Performance;

namespace Inno.VisualNovel.Model.Programs;

/// <summary>Creates isolated runtime-owned program data without requiring a serializer at runtime.</summary>
public static class StoryProgramCopy
{
    /// <summary>Copies the complete executable input so later asset or caller edits cannot affect a running session.</summary>
    /// <param name="source">The compiled program.</param>
    /// <returns>An independently mutable program owned by the caller.</returns>
    public static StoryProgram Copy(StoryProgram source) => new()
    {
        chapterId = source.chapterId,
        fingerprint = source.fingerprint,
        entries = source.entries.Select(static entry => new StoryEntry { id = entry.id, nodeId = entry.nodeId }).ToArray(),
        variables = source.variables.Select(VariableValueRules.Copy).ToArray(),
        instructions = source.instructions.Select(static item => new StoryInstruction
        {
            nodeId = item.nodeId, definitionId = item.definitionId,
            log = item.log is { } log ? new Story.Nodes.Debugging.LogNode { message = log.message, level = log.level, previewOnly = log.previewOnly } : null,
            system = item.system is { } system ? new SystemNode { operation = system.operation, argument = system.argument } : null,
            unlock = item.unlock is { } unlock ? new UnlockNode { entryId = unlock.entryId } : null,
            command = item.command is { } command ? Model.Extensions.CommandContract.Copy(command) : null,
            parallel = item.parallel is { } parallel ? new ParallelNode { joinId = parallel.joinId, branches = parallel.branches.ToArray(), waitAny = parallel.waitAny } : null,
            join = item.join is { } join ? new JoinNode { forkId = join.forkId } : null,
            screenEffect = item.screenEffect is { } effect ? new ScreenEffectNode { operation = effect.operation, resourceId = effect.resourceId, instanceId = effect.instanceId,
                strength = effect.strength, duration = effect.duration, order = effect.order, includeUi = effect.includeUi, waitForCompletion = effect.waitForCompletion } : null,
            transition = item.transition is { } transition ? new TransitionNode { operation = transition.operation, resourceId = transition.resourceId, nextStage = transition.nextStage,
                scope = transition.scope, group = transition.group, target = transition.target, waitForCompletion = transition.waitForCompletion } : null,
            sourceChapterId = item.sourceChapterId, sourceNodeId = item.sourceNodeId,
            targets = item.targets.Select(static target => new InstructionTarget { port = target.port, nodeId = target.nodeId }).ToArray(),
            dialogue = item.dialogue is { } line ? new DialogueNode { textId = line.textId, characterId = line.characterId, speaker = line.speaker, text = line.text, voiceCueId = line.voiceCueId, charactersPerSecond = line.charactersPerSecond, punctuationPause = line.punctuationPause,
                presentation = line.presentation, clearPage = line.clearPage, anchorX = line.anchorX, anchorY = line.anchorY, relativeWidth = line.relativeWidth } : null,
            input = item.input is { } input ? new InputNode { prompt = input.prompt, variableId = input.variableId, defaultText = input.defaultText, minimumLength = input.minimumLength, maximumLength = input.maximumLength, allowCancel = input.allowCancel } : null,
            choice = item.choice is { } choice ? new ChoiceNode
            {
                prompt = choice.prompt, timeout = choice.timeout, defaultOption = choice.defaultOption,
                options = choice.options.Select(static option => new ChoiceOption
                {
                    id = option.id, text = option.text, disabledReason = option.disabledReason,
                    enabled = CopyExpression(option.enabled), visible = CopyExpression(option.visible)
                }).ToArray()
            } : null,
            condition = item.condition is { } condition ? new ConditionNode { condition = CopyExpression(condition.condition) } : null,
            assignment = item.assignment is { } assignment ? new AssignmentNode { variableId = assignment.variableId, value = CopyExpression(assignment.value) } : null,
            wait = item.wait is { } wait ? new WaitNode { seconds = wait.seconds, waitForInput = wait.waitForInput } : null,
            call = item.call is { } call ? new CallNode { entryId = call.entryId, chapterId = call.chapterId, jump = call.jump } : null,
            random = item.random is { } random ? new RandomNode { branches = random.branches.Select(static branch => new WeightedBranch { id = branch.id, weight = branch.weight }).ToArray() } : null,
            audio = item.audio is { } audio ? new AudioNode { operation = audio.operation, slot = audio.slot, cueId = audio.cueId } : null,
            performance = item.performance is { } performance ? new PerformanceNode { operation = performance.operation, instanceId = performance.instanceId, clipId = performance.clipId, time = performance.time, markerId = performance.markerId,
                bindings = performance.bindings.Select(static item => new PerformanceBinding { slot = item.slot, target = item.target }).ToArray() } : null,
            stage = item.stage is { } stage ? new StageNode
            {
                operation = stage.operation, resourceId = stage.resourceId, target = stage.target, pose = stage.pose, expression = stage.expression, order = stage.order, seed = stage.seed,
                transform = stage.transform.Copy()
            } : null
        }).ToArray()
    };

    private static ExpressionDocument CopyExpression(
        ExpressionDocument source,
        int depth = 0
    ) => depth <= 64 ? new()
    {
        operation = source.operation, variableId = source.variableId,
        literal = source.literal.Copy(), operands = source.operands.Select(item => CopyExpression(item, depth + 1)).ToArray()
    } : throw new InvalidOperationException("Expression exceeds the supported depth.");
}

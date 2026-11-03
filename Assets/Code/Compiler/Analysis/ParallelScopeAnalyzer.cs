using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Graphs;
using Inno.VisualNovel.Model.Extensions;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Audio;
using Inno.VisualNovel.Model.Story.Nodes.Extensions;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Model.Story.Nodes.Variables;
using Inno.VisualNovel.Model.Story.Nodes.Reading;

namespace Inno.VisualNovel.Compiler.Analysis;

internal sealed class ParallelScopeAnalyzer(StoryNodeDataCodec codec)
{
    internal IReadOnlyList<(string nodeId, FormattableString message)> Analyze(StoryAsset story)
    {
        var issues = new List<(string, FormattableString)>();
        var graph = story.graph.document;
        var nodes = graph.nodes.ToDictionary(static node => node.id.value, StringComparer.Ordinal);
        var outgoing = graph.edges.GroupBy(static edge => edge.output.nodeId.value).ToDictionary(static group => group.Key, static group => group.ToArray(), StringComparer.Ordinal);
        var incoming = graph.edges.GroupBy(static edge => edge.input.nodeId.value).ToDictionary(static group => group.Key, static group => group.ToArray(), StringComparer.Ordinal);
        var ownedJoins = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fork in graph.nodes.Where(static node => node.definitionId == StoryNodeIds.parallel))
        {
            ParallelNode definition = codec.Read<ParallelNode>(fork);
            if (definition.branches.Length is < 2 or > 16 || definition.branches.Any(string.IsNullOrWhiteSpace) || definition.branches.Distinct(StringComparer.Ordinal).Count() != definition.branches.Length
                || !nodes.TryGetValue(definition.joinId, out var join) || join.definitionId != StoryNodeIds.join || codec.Read<JoinNode>(join).forkId != fork.id.value || !ownedJoins.Add(definition.joinId))
            { Error(fork.id.value, $"A parallel group requires 2–16 unique branches and one exclusively owned, matching join."); continue; }
            if (story.entries.Any(entry => entry.nodeId == definition.joinId)) Error(definition.joinId, $"A parallel join cannot be a public chapter entry.");
            var occupied = new HashSet<string>(StringComparer.Ordinal);
            var resourceOwners = new Dictionary<string, string>(StringComparer.Ordinal);
            string? interactive = null;
            foreach (string branch in definition.branches)
            {
                var entry = outgoing.GetValueOrDefault(fork.id.value)?.SingleOrDefault(edge => edge.output.portId.value == branch)?.input.nodeId.value;
                if (entry is null) continue;
                var members = new HashSet<string>(StringComparer.Ordinal); var pending = new Stack<string>(); pending.Push(entry); bool reached = false;
                while (pending.TryPop(out string? id))
                {
                    if (id == definition.joinId) { reached = true; continue; }
                    if (id == fork.id.value) { Error(id, $"A parallel branch cannot return to its own fork before joining."); continue; }
                    if (!members.Add(id) || !nodes.TryGetValue(id, out var node)) continue;
                    if (outgoing.TryGetValue(id, out var edges)) foreach (var edge in edges) pending.Push(edge.input.nodeId.value);
                    if (node.definitionId is StoryNodeIds.system or StoryNodeIds.unlock or StoryNodeIds.call or StoryNodeIds.returnFromCall or StoryNodeIds.end)
                        Error(id, $"Place page changes, unlocks, cross-chapter calls and early completion after the parallel join.");
                    if (node.definitionId == StoryNodeIds.command)
                    {
                        CommandNode command = codec.Read<CommandNode>(node);
                        if (command.resultVariable.Length > 0 || command.definition.capabilities.HasFlag(CommandCapabilities.ExternalEffects)
                            || !command.definition.capabilities.HasFlag(CommandCapabilities.Save | CommandCapabilities.Rollback))
                            Error(id, $"A parallel extension must support saving and rollback, have no external effects and not write a narrative result variable.");
                    }
                }
                if (!reached) Error(fork.id.value, $"The parallel branch has no path to its own join: {branch}");
                bool inputOwner = members.Any(id => nodes.TryGetValue(id, out var member)
                    && (member.definitionId is StoryNodeIds.dialogue or StoryNodeIds.choice or StoryNodeIds.input
                        || member.definitionId == StoryNodeIds.wait && codec.Read<WaitNode>(member).waitForInput));
                if (inputOwner && interactive is not null) Error(fork.id.value, $"Only one parallel branch can own dialogue, choices or player input focus: {interactive} / {branch}");
                if (inputOwner) interactive = branch;
                foreach (string id in members)
                {
                    if (!occupied.Add(id)) Error(id, $"Parallel tasks may merge only at their own join.");
                    if (story.entries.Any(entryPoint => entryPoint.nodeId == id)) Error(id, $"An internal parallel node cannot also be a public chapter entry.");
                    foreach (var edge in incoming.GetValueOrDefault(id) ?? [])
                        if (!members.Contains(edge.output.nodeId.value) && edge.output.nodeId != fork.id) Error(id, $"An execution connection crosses a parallel group or branch boundary.");
                    foreach (string resource in Writes(nodes[id]))
                        if (resourceOwners.TryGetValue(resource, out string? owner) && owner != branch) Error(id, $"Parallel tasks write to the same variable or performance target: {resource}");
                        else resourceOwners[resource] = branch;
                }
            }
            foreach (var edge in incoming.GetValueOrDefault(definition.joinId) ?? [])
                if (!occupied.Contains(edge.output.nodeId.value) && edge.output.nodeId != fork.id) Error(definition.joinId, $"The join receives an execution connection from outside its parallel group.");
        }
        foreach (var join in graph.nodes.Where(static node => node.definitionId == StoryNodeIds.join))
            if (!ownedJoins.Contains(join.id.value)) Error(join.id.value, $"The join has no matching parallel fork.");
        return issues;
        void Error(
            string id,
            FormattableString text
        ) => issues.Add((id, text));
    }
    private IEnumerable<string> Writes(GraphNodeRecord node)
    {
        if (node.definitionId == StoryNodeIds.dialogue) yield return "audio/voice";
        if (node.definitionId == StoryNodeIds.assign) yield return "variable/" + codec.Read<AssignmentNode>(node).variableId;
        if (node.definitionId == StoryNodeIds.input) yield return "variable/" + codec.Read<InputNode>(node).variableId;
        if (node.definitionId == StoryNodeIds.screenEffect)
        {
            var effect = codec.Read<ScreenEffectNode>(node); if (effect.operation != ScreenEffectOperation.Wait) yield return "effect/" + effect.instanceId;
        }
        if (node.definitionId == StoryNodeIds.transition)
        {
            var transition = codec.Read<TransitionNode>(node);
            if (transition.operation != TransitionOperation.Wait) yield return "transition";
            if (transition.nextStage.Length > 0) yield return "scenery";
        }
        if (node.definitionId == StoryNodeIds.audio)
        {
            var audio = codec.Read<AudioNode>(node); if (audio.operation != AudioOperation.Wait) yield return "audio/" + audio.slot;
        }
        if (node.definitionId == StoryNodeIds.performance)
        {
            var performance = codec.Read<PerformanceNode>(node); if (performance.operation is not (Model.Performance.PerformanceOperation.Wait or Model.Performance.PerformanceOperation.WaitMarker)) yield return "performance/" + performance.instanceId;
        }
        if (node.definitionId == StoryNodeIds.stage)
        {
            var stage = codec.Read<StageNode>(node);
            if (stage.operation is StageOperation.SetCamera or StageOperation.FollowCamera or StageOperation.StopCameraFollow) yield return "camera";
            else if (stage.operation == StageOperation.SetStage) yield return "scenery";
            else if (stage.operation is StageOperation.ShowCharacter or StageOperation.HideCharacter or StageOperation.BeginSpeaking or StageOperation.EndSpeaking) yield return "character/" + stage.target;
            else if (stage.operation != StageOperation.WaitParticles) yield return "particles/" + stage.target;
        }
    }
}

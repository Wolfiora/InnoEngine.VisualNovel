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
            { Error(fork.id.value, $"并行组需要 2～16 个唯一分支和一个独占、互相匹配的汇合点。"); continue; }
            if (story.entries.Any(entry => entry.nodeId == definition.joinId)) Error(definition.joinId, $"并行汇合点不能作为公开章节入口。");
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
                    if (id == fork.id.value) { Error(id, $"并行分支不能在汇合前返回自己的起点。"); continue; }
                    if (!members.Add(id) || !nodes.TryGetValue(id, out var node)) continue;
                    if (outgoing.TryGetValue(id, out var edges)) foreach (var edge in edges) pending.Push(edge.input.nodeId.value);
                    if (node.definitionId is StoryNodeIds.system or StoryNodeIds.unlock or StoryNodeIds.call or StoryNodeIds.returnFromCall or StoryNodeIds.end)
                        Error(id, $"并行分支中的页面切换、解锁、跨章节调用或提前结束需放在汇合后。");
                    if (node.definitionId == StoryNodeIds.command)
                    {
                        CommandNode command = codec.Read<CommandNode>(node);
                        if (command.resultVariable.Length > 0 || command.definition.capabilities.HasFlag(CommandCapabilities.ExternalEffects)
                            || !command.definition.capabilities.HasFlag(CommandCapabilities.Save | CommandCapabilities.Rollback))
                            Error(id, $"并行扩展必须可保存、可回滚、没有外部副作用且不写剧情结果变量。");
                    }
                }
                if (!reached) Error(fork.id.value, $"并行分支没有通向所属汇合点的路径：{branch}");
                bool inputOwner = members.Any(id => nodes.TryGetValue(id, out var member)
                    && (member.definitionId is StoryNodeIds.dialogue or StoryNodeIds.choice or StoryNodeIds.input
                        || member.definitionId == StoryNodeIds.wait && codec.Read<WaitNode>(member).waitForInput));
                if (inputOwner && interactive is not null) Error(fork.id.value, $"并行组只允许一个分支拥有对白、选项或玩家输入焦点：{interactive} / {branch}");
                if (inputOwner) interactive = branch;
                foreach (string id in members)
                {
                    if (!occupied.Add(id)) Error(id, $"不同并行任务只能在所属汇合点合流。");
                    if (story.entries.Any(entryPoint => entryPoint.nodeId == id)) Error(id, $"并行内部节点不能同时作为公开章节入口。");
                    foreach (var edge in incoming.GetValueOrDefault(id) ?? [])
                        if (!members.Contains(edge.output.nodeId.value) && edge.output.nodeId != fork.id) Error(id, $"执行连接跨越了并行组或分支边界。");
                    foreach (string resource in Writes(nodes[id]))
                        if (resourceOwners.TryGetValue(resource, out string? owner) && owner != branch) Error(id, $"并行任务同时写入同一变量或演出目标：{resource}");
                        else resourceOwners[resource] = branch;
                }
            }
            foreach (var edge in incoming.GetValueOrDefault(definition.joinId) ?? [])
                if (!occupied.Contains(edge.output.nodeId.value) && edge.output.nodeId != fork.id) Error(definition.joinId, $"汇合点收到不属于本并行组的执行连接。");
        }
        foreach (var join in graph.nodes.Where(static node => node.definitionId == StoryNodeIds.join))
            if (!ownedJoins.Contains(join.id.value)) Error(join.id.value, $"汇合点缺少匹配的并行起点。");
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

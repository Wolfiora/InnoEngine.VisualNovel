using System;
using System.Collections.Generic;
using Inno.VisualNovel.Model.Extensions;
using Inno.VisualNovel.Model.Programs;
using Inno.VisualNovel.Model.Story;

namespace Inno.VisualNovel.Narrative.Execution;

internal sealed class StoryParallelScope
{
    private readonly HashSet<string> m_nodes = new(StringComparer.Ordinal);
    private readonly string m_join;
    internal bool hasDialogue { get; }
    internal StoryParallelScope(
        IReadOnlyDictionary<string, StoryInstruction> instructions,
        string start,
        string join
    ) {
        m_join = join;
        var pending = new Stack<string>(); pending.Push(start);
        while (pending.TryPop(out string? id))
            if (id != join && m_nodes.Add(id)) foreach (var target in instructions[id].targets) pending.Push(target.nodeId);
        foreach (string id in m_nodes) if (instructions[id].definitionId == StoryNodeIds.dialogue) hasDialogue = true;
    }
    internal bool Contains(string id) => id == m_join || m_nodes.Contains(id);
    internal void Validate(StoryInstruction instruction)
    {
        if (!Contains(instruction.nodeId)
            || instruction.definitionId is StoryNodeIds.system or StoryNodeIds.unlock or StoryNodeIds.call or StoryNodeIds.returnFromCall or StoryNodeIds.end)
            throw new InvalidOperationException("Parallel task escaped its bounded execution scope.");
        if (instruction.command is { } command && (command.resultVariable.Length > 0 || command.definition.capabilities.HasFlag(CommandCapabilities.ExternalEffects)
            || !command.definition.capabilities.HasFlag(CommandCapabilities.Save | CommandCapabilities.Rollback)))
            throw new InvalidOperationException("Parallel command lacks safe continuation guarantees.");
    }
    internal static void ValidateFork(
        StoryInstruction fork,
        IReadOnlyDictionary<string, StoryInstruction> instructions
    ) {
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        string? interactive = null;
        foreach (string branch in fork.parallel!.branches)
        {
            string start = Array.Find(fork.targets, target => target.port == branch)?.nodeId ?? throw new InvalidOperationException("Parallel branch has no target: " + branch);
            var scope = new StoryParallelScope(instructions, start, fork.parallel.joinId);
            bool ownsInput = false;
            foreach (string id in scope.m_nodes)
            {
                var instruction = instructions[id];
                ownsInput |= instruction.definitionId is StoryNodeIds.dialogue or StoryNodeIds.choice or StoryNodeIds.input;
                string? variable = instruction.assignment?.variableId ?? instruction.input?.variableId;
                if (variable is null) continue;
                if (owners.TryGetValue(variable, out string? previous) && previous != branch)
                    throw new InvalidOperationException("Parallel variable has competing writers: " + variable + " (" + previous + ", " + branch + ").");
                owners[variable] = branch;
            }
            if (ownsInput && interactive is not null) throw new InvalidOperationException("Parallel branches compete for interactive input: " + interactive + ", " + branch + ".");
            if (ownsInput) interactive = branch;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Narrative.Debugging;

namespace Inno.VisualNovel.Narrative.Execution;

internal sealed class StoryParallelExecution : IDisposable
{
    private readonly List<(string branch, StoryMachine machine)> m_tasks = [];
    private readonly ParallelNode m_definition;
    internal StoryParallelExecution(
        ParallelNode definition,
        Func<string, StoryExecutionState?, StoryMachine> create,
        StoryTaskState[]? restored = null
    ) {
        if (definition.branches.Length is < 2 or > 16 || definition.branches.Distinct(StringComparer.Ordinal).Count() != definition.branches.Length
            || restored is not null && (restored.Length != definition.branches.Length || restored.Where((task, index) => task.branch != definition.branches[index]).Any()))
            throw new InvalidOperationException("Parallel snapshot does not match its stable branch identities.");
        m_definition = definition;
        try
        {
            for (int index = 0; index < definition.branches.Length; index++)
                m_tasks.Add((definition.branches[index], create(definition.branches[index], restored?[index].execution)));
            CheckFaults();
        }
        catch { Dispose(); throw; }
    }
    internal bool canSave => m_tasks.All(static task => task.machine.canSave);
    internal StoryMachine? interactive => m_tasks.Select(static task => task.machine.interactiveOwner).FirstOrDefault(static machine => machine is not null);
    internal StoryMachine? completedReading => m_tasks.FirstOrDefault(static task => task.machine.status == StoryStatus.Completed && task.machine.hasTaskDialogue).machine;
    internal IEnumerable<StoryMachine> InspectTasks() => m_tasks.SelectMany(static task => task.machine.InspectTasks());
    internal void UseDebugger(
        StoryDebugger? debugger,
        string path
    ) { foreach (var task in m_tasks) task.machine.UseDebugger(debugger, path + "/" + m_definition.joinId + ":" + task.branch); }
    internal void SetTextSource(Reading.StoryTextSource source) { foreach (var task in m_tasks) task.machine.SetTextSource(source); }
    internal void RefreshLocalizedDialogue() { foreach (var task in m_tasks) task.machine.RefreshLocalizedDialogue(); }
    internal IReadOnlyList<StoryMachine> presentations => m_tasks.SelectMany(static task => task.machine.PresentationTasks()).ToArray();
    internal bool complete => m_definition.waitAny ? m_tasks.Any(static task => task.machine.status == StoryStatus.Completed) : m_tasks.All(static task => task.machine.status == StoryStatus.Completed);
    internal void Tick(
        double seconds,
        double readingSpeed
    )
    {
        foreach (var task in m_tasks) task.machine.Tick(seconds, readingSpeed);
        CheckFaults();
    }
    internal void Synchronize()
    {
        foreach (var task in m_tasks) task.machine.SynchronizeTasks();
        CheckFaults();
    }
    internal StoryTaskState[] Capture() => m_tasks.Select(static task => new StoryTaskState { branch = task.branch, execution = task.machine.Capture() }).ToArray();
    internal StoryOwnedResource[] CollectResources(bool cancelled) => m_tasks.Where(task => cancelled ? task.machine.status != StoryStatus.Completed : task.machine.status == StoryStatus.Completed)
        .SelectMany(static task => task.machine.AllOwnedResources()).DistinctBy(static resource => (resource.kind, resource.id)).ToArray();
    internal StoryOwnedResource[] DrainCleanup() => m_tasks.SelectMany(static task => task.machine.DrainTaskCleanup()).ToArray();
    private void CheckFaults()
    {
        var failed = m_tasks.FirstOrDefault(static task => task.machine.status is StoryStatus.Faulted or StoryStatus.Stopped);
        if (failed.machine is not null) throw new InvalidOperationException("Parallel branch " + failed.branch + ": " + failed.machine.error);
    }
    public void Dispose() { foreach (var task in m_tasks) task.machine.Dispose(); m_tasks.Clear(); }
}

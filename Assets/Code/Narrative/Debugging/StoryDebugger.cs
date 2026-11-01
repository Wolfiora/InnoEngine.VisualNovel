using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Narrative.Execution;

namespace Inno.VisualNovel.Narrative.Debugging;

/// <summary>Observes one interpreter tree and provides source breakpoints without changing saved game data.</summary>
public sealed class StoryDebugger
{
    private readonly int m_capacity;
    private readonly Queue<StoryTraceEntry> m_trace = [];
    private readonly HashSet<(string chapter, string node)> m_nodes = [];
    private readonly HashSet<(string chapter, string node, string output)> m_edges = [];
    private StoryBreakpoint[] m_breakpoints = [];
    private StoryMachine? m_stopped;
    private StoryMachine? m_root;
    private StoryMachine? m_skip;
    private string m_skipNode = string.Empty;
    private bool m_before;
    private int m_remaining = -1;
    private long m_sequence;
    /// <summary>Creates a bounded trace owner for one preview session.</summary>
    /// <param name="traceCapacity">The retained entry and edge count, from 1 to 10000.</param>
    public StoryDebugger(int traceCapacity = 1024)
    {
        if (traceCapacity is < 1 or > 10000) throw new ArgumentOutOfRangeException(nameof(traceCapacity));
        m_capacity = traceCapacity;
    }
    /// <summary>Gets whether all narrative tasks and presentation time are held for inspection.</summary>
    public bool paused { get; private set; }
    /// <summary>Gets the current stop reason, including condition evaluation errors.</summary>
    public string reason { get; private set; } = string.Empty;
    /// <summary>Gets the stopped source and task, or null before a stop.</summary>
    public StoryDebugLocation? location { get; private set; }
    /// <summary>Gets a defensive ordered copy of the retained execution trace.</summary>
    public IReadOnlyList<StoryTraceEntry> trace => Array.AsReadOnly(m_trace.ToArray());
    /// <summary>Gets the number of distinct authored instructions entered in this debug run.</summary>
    public int coveredNodes => m_nodes.Count;
    /// <summary>Gets the number of distinct authored output ports taken in this debug run.</summary>
    public int coveredOutputs => m_edges.Count;
    /// <summary>Gets whether source coverage exceeded its 100000-node or 100000-output budget.</summary>
    public bool coverageTruncated { get; private set; }
    /// <summary>Gets current source breakpoints without exposing a mutable collection.</summary>
    public IReadOnlyList<StoryBreakpoint> breakpoints => Array.AsReadOnly(m_breakpoints.ToArray());
    /// <summary>Gets the task held at the current stop for explicit variable inspection.</summary>
    public StoryMachine? stoppedTask => paused ? m_stopped : null;
    /// <summary>Replaces source breakpoints atomically and rejects duplicate source identities.</summary>
    /// <param name="breakpoints">At most 10000 source breakpoints.</param>
    public void SetBreakpoints(IEnumerable<StoryBreakpoint> breakpoints)
    {
        StoryBreakpoint[] candidate = breakpoints.Take(10001).ToArray();
        if (candidate.Length > 10000 || candidate.Any(static item => item is null)
            || candidate.Select(static item => (item.chapter, item.node)).Distinct().Count() != candidate.Length)
            throw new ArgumentException("Breakpoints need unique bounded source identities.", nameof(breakpoints));
        m_breakpoints = candidate;
    }
    /// <summary>Holds a live task without consuming input, elapsed time or narrative random state.</summary>
    /// <param name="machine">A task owned by this debugger's session.</param>
    public void Pause(StoryMachine machine)
    {
        if (machine.status == StoryStatus.Stopped) throw new InvalidOperationException("Start the interpreter before pausing it.");
        if (!machine.UsesDebugger(this)) throw new InvalidOperationException("The task belongs to another debugger.");
        Stop(machine, "用户暂停", machine.status == StoryStatus.Running);
    }
    /// <summary>Continues execution, skipping only the exact breakpoint just stopped before once.</summary>
    /// <param name="singleInstruction">Whether to stop after one instruction or at its next natural wait.</param>
    public void Continue(bool singleInstruction = false)
    {
        m_skip = m_before ? m_stopped : null; m_skipNode = location?.runtimeNode ?? string.Empty;
        paused = false; reason = string.Empty; m_stopped = null; m_remaining = singleInstruction ? 1 : -1;
    }
    /// <summary>Tests source coverage without returning mutable runtime data.</summary>
    /// <param name="chapter">The original chapter identity.</param>
    /// <param name="node">The authored node identity.</param>
    /// <returns>Whether an instruction mapped to this source has entered.</returns>
    public bool HasVisited(
        string chapter,
        string node
    ) => m_nodes.Contains((chapter, node));
    /// <summary>Clears trace and coverage while retaining authored breakpoint configuration.</summary>
    public void ClearTrace() { m_trace.Clear(); m_nodes.Clear(); m_edges.Clear(); m_sequence = 0; coverageTruncated = false; }
    internal bool Before(StoryMachine machine)
    {
        if (paused) return false;
        bool skip = ReferenceEquals(m_skip, machine) && m_skipNode == machine.nodeId;
        if (skip) { m_skip = null; m_skipNode = string.Empty; }
        StoryDebugLocation source = machine.debugLocation;
        if (m_remaining == 0) { Stop(machine, "单步：下一条指令", true); return false; }
        if (!skip && m_breakpoints.SingleOrDefault(item => item.chapter == source.chapter && item.node == source.node) is { } breakpoint)
        {
            try { if (breakpoint.Matches(machine.GetVariable)) { Stop(machine, "命中断点", true); return false; } }
            catch (Exception error) when (error is not OutOfMemoryException) { Stop(machine, "断点条件错误：" + error.Message, true); return false; }
        }
        if (m_remaining > 0) m_remaining--;
        if (m_nodes.Count < 100000) m_nodes.Add((source.chapter, source.node));
        else if (!m_nodes.Contains((source.chapter, source.node))) coverageTruncated = true;
        Add(source, string.Empty, string.Empty); return true;
    }
    internal void After(StoryMachine machine)
    {
        if (!paused && m_remaining == 0 && machine.status is not (StoryStatus.Running or StoryStatus.Presentation or StoryStatus.Parallel))
            Stop(machine, "单步：" + machine.status, false);
    }
    internal void Edge(
        StoryMachine machine,
        string port,
        string target
    ) {
        StoryDebugLocation source = machine.debugLocation;
        if (m_edges.Count < 100000) m_edges.Add((source.chapter, source.node, port));
        else if (!m_edges.Contains((source.chapter, source.node, port))) coverageTruncated = true;
        Add(source, port, target);
    }
    internal void Bind(StoryMachine root)
    {
        if (m_root is not null && !ReferenceEquals(m_root, root)) throw new InvalidOperationException("A debugger already belongs to another live interpreter tree.");
        m_root = root;
    }
    internal void Detach(StoryMachine root)
    {
        if (!ReferenceEquals(m_root, root)) return;
        m_root = null;
        m_stopped = m_skip = null; location = null; paused = false; m_before = false; m_remaining = -1; reason = string.Empty;
    }
    private void Stop(
        StoryMachine machine,
        string cause,
        bool before
    ) {
        if (paused) return;
        m_stopped = machine; location = machine.debugLocation; paused = true; reason = cause; m_before = before;
    }
    private void Add(
        StoryDebugLocation source,
        string port,
        string target
    ) {
        if (m_trace.Count >= m_capacity) m_trace.Dequeue();
        m_trace.Enqueue(new(checked(++m_sequence), source, port, target));
    }
}

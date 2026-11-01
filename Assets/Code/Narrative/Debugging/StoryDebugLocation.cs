namespace Inno.VisualNovel.Narrative.Debugging;

/// <summary>Locates one linked instruction without exposing executable mutable data.</summary>
/// <param name="task">The stable path within the bounded parallel task tree.</param>
/// <param name="chapter">The original authored chapter identity.</param>
/// <param name="node">The original graph node identity.</param>
/// <param name="runtimeNode">The lowered executable identity.</param>
/// <param name="definition">The stable instruction definition.</param>
public sealed record StoryDebugLocation(
    string task,
    string chapter,
    string node,
    string runtimeNode,
    string definition
);

/// <summary>Records an entered instruction or an accepted control-flow edge.</summary>
/// <param name="sequence">The monotonic debugger-local ordering.</param>
/// <param name="location">The source location of the entry or branch.</param>
/// <param name="output">The selected output, empty for instruction entry.</param>
/// <param name="target">The linked destination, empty for instruction entry.</param>
public sealed record StoryTraceEntry(
    long sequence,
    StoryDebugLocation location,
    string output,
    string target
);

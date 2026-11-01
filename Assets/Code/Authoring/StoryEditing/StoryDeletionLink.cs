using InnoEngine.Graphs;

namespace Inno.VisualNovel.Authoring.StoryEditing;

/// <summary>Freezes one original connection affected by a reviewed group deletion.</summary>
/// <param name="id">The original stable edge identity.</param>
/// <param name="output">The source node and execution port.</param>
/// <param name="input">The destination node and execution port.</param>
public sealed record StoryDeletionLink(
    GraphEdgeId id,
    GraphEndpoint output,
    GraphEndpoint input
);

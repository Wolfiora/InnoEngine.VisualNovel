using InnoEngine.Graphs;

namespace Inno.VisualNovel.Authoring.StoryEditing;

/// <summary>Describes one reviewed execution connection without retaining mutable graph objects.</summary>
/// <param name="id">The existing stable connection identity.</param>
/// <param name="beforeOutput">The original source and port.</param>
/// <param name="beforeInput">The original destination and port.</param>
/// <param name="afterOutput">The proposed source and port.</param>
/// <param name="afterInput">The proposed destination and port.</param>
public sealed record StoryConnectionChange(
    GraphEdgeId id,
    GraphEndpoint beforeOutput,
    GraphEndpoint beforeInput,
    GraphEndpoint afterOutput,
    GraphEndpoint afterInput
);

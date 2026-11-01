using InnoEngine.Graphs;

namespace Inno.VisualNovel.Authoring.StoryEditing;

/// <summary>Describes one newly authored instruction and whether its explicit continuation was connected.</summary>
/// <param name="nodeId">The new instruction or group entry identity.</param>
/// <param name="connected">Whether creation replaced the selected next edge with a complete control-flow template.</param>
public sealed record StoryInsertionResult(
    GraphNodeId nodeId,
    bool connected
);

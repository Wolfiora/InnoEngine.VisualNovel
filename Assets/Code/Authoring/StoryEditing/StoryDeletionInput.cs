using InnoEngine.Graphs;

namespace Inno.VisualNovel.Authoring.StoryEditing;

/// <summary>Describes an outside source entering deleted nodes and its provably linear continuation, if any.</summary>
/// <param name="id">The existing incoming edge identity retained when reconnected.</param>
/// <param name="output">The unaffected source execution endpoint.</param>
/// <param name="previousTarget">The original destination inside the removed group.</param>
/// <param name="suggestedTarget">A unique ordinary linear continuation, empty when an explicit decision is required.</param>
public sealed record StoryDeletionInput(
    GraphEdgeId id,
    GraphEndpoint output,
    string previousTarget,
    string suggestedTarget
);

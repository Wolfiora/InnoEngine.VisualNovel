using System;
using System.Collections.Generic;

namespace Inno.VisualNovel.Authoring.StoryEditing;

/// <summary>Owns a source-addressed proposal without retaining graph objects or guessing junction reconnections.</summary>
public sealed class StoryDeletionReview
{
    internal StoryDeletionReview(
        string chapterId,
        string[] nodes,
        StoryDeletionLink[] connections,
        StoryDeletionInput[] inputs
    ) {
        this.chapterId = chapterId; this.nodes = Array.AsReadOnly(nodes);
        this.connections = Array.AsReadOnly(connections); this.inputs = Array.AsReadOnly(inputs);
    }
    /// <summary>Gets the owning chapter identity.</summary>
    public string chapterId { get; }
    /// <summary>Gets exact removed node identities in authored order.</summary>
    public IReadOnlyList<string> nodes { get; }
    /// <summary>Gets every original affected connection, including internal and external links.</summary>
    public IReadOnlyList<StoryDeletionLink> connections { get; }
    /// <summary>Gets outside sources that require a continuation or explicit disconnection.</summary>
    public IReadOnlyList<StoryDeletionInput> inputs { get; }
}

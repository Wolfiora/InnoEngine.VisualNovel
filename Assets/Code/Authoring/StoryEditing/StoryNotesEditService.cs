using System;
using System.Linq;
using InnoEngine.Graphs;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;

namespace Inno.VisualNovel.Authoring.StoryEditing;

/// <summary>Edits bounded author annotations inside the authoritative native graph, including Missing nodes.</summary>
public sealed class StoryNotesEditService(IStorySerialization serialization)
{
    private const string C_KEY = "inno.vn.author-notes";
    /// <summary>Reads detached annotations without interpreting a node's unknown settings.</summary>
    /// <param name="node">The existing source node.</param>
    /// <returns>A fresh annotation value, defaulting to no notes or bookmark.</returns>
    public StoryNodeNotes Read(GraphNodeRecord node) => node.TryGetValue(C_KEY, out var value) && value is not null
        ? serialization.Deserialize<StoryNodeNotes>(value.data.Span) : new();
    /// <summary>Writes notes through the caller's ordinary document transaction without changing execution or identity.</summary>
    /// <param name="draft">The detached transaction candidate.</param>
    /// <param name="nodeId">The stable node identity.</param>
    /// <param name="notes">The complete proposed annotation.</param>
    public void Set(
        StoryDraftState draft,
        string nodeId,
        StoryNodeNotes notes
    ) {
        var node = draft.graph.document.FindNode(new(nodeId)) ?? throw new InvalidOperationException("The annotated node no longer exists: " + nodeId);
        if (notes.note.Length > 32768 || notes.tags.Length > 32 || notes.tags.Any(static tag => string.IsNullOrWhiteSpace(tag) || tag.Length > 64 || tag != tag.Trim() || tag.Any(char.IsControl))
            || notes.tags.Distinct(StringComparer.OrdinalIgnoreCase).Count() != notes.tags.Length)
            throw new InvalidOperationException("Notes are limited to 32768 characters and 32 unique nonempty tags of at most 64 characters; tags cannot contain control characters.");
        node.SetValue(C_KEY, new(serialization.Serialize(notes)));
    }
}

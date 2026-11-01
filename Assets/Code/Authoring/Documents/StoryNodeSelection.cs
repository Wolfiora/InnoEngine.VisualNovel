using System;
using InnoEditor.Interactions;

namespace Inno.VisualNovel.Authoring.Documents;

/// <summary>Publishes a document-qualified primary node through the engine selection service without retaining plugin objects.</summary>
public static class StoryNodeSelection
{
    private const string C_PREFIX = "inno.vn.node/";

    /// <summary>Selects one primary node using a reload-neutral string owned by the engine coordinator.</summary>
    /// <param name="interactions">The current editor generation's interaction service.</param>
    /// <param name="documentId">The shared story document identity.</param>
    /// <param name="nodeId">The stable node identity; empty clears only this document's node selection.</param>
    public static void Select(
        EditorInteractions interactions,
        Guid documentId,
        string nodeId
    ) {
        if (documentId == Guid.Empty) throw new ArgumentException("A shared document identity is required.", nameof(documentId));
        if (nodeId.Length == 0)
        {
            if (TryRead(interactions, documentId, out _)) interactions.SetSelection(null);
            return;
        }
        interactions.SetSelection(C_PREFIX + documentId.ToString("N") + "/" + nodeId);
    }

    /// <summary>Reads a primary node only when the global selection belongs to the requested document.</summary>
    /// <param name="interactions">The native interaction service.</param>
    /// <param name="documentId">The shared story document identity.</param>
    /// <param name="nodeId">Receives the node's complete stable identity.</param>
    /// <returns>Whether the current selection targets this story document.</returns>
    public static bool TryRead(
        EditorInteractions interactions,
        Guid documentId,
        out string nodeId
    ) {
        string prefix = C_PREFIX + documentId.ToString("N") + "/";
        if (interactions.selection.selectedTarget is string address && address.StartsWith(prefix, StringComparison.Ordinal) && address.Length > prefix.Length)
        { nodeId = address[prefix.Length..]; return true; }
        nodeId = string.Empty; return false;
    }
}

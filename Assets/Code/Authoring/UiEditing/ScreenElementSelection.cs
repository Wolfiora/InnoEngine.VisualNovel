using System;
using System.Linq;
using InnoEditor.Interactions;
using Inno.VisualNovel.Model.Interface;

namespace Inno.VisualNovel.Authoring.UiEditing;

/// <summary>Shares document-qualified UI element identities through the engine's reload-neutral selection coordinator.</summary>
public static class ScreenElementSelection
{
    private const string C_PREFIX = "inno.vn.ui/";
    /// <summary>Selects one authored element without retaining a plugin object in global editor state.</summary>
    /// <param name="interactions">The current editor coordinator.</param>
    /// <param name="documentId">The native screen document identity.</param>
    /// <param name="elementId">The stable authored element, or empty to clear this document's UI selection.</param>
    public static void Select(
        EditorInteractions interactions,
        Guid documentId,
        string elementId
    ) {
        if (documentId == Guid.Empty) throw new ArgumentException("A native screen document identity is required.", nameof(documentId));
        if (elementId.Length == 0) { if (TryRead(interactions, documentId, out _)) interactions.SetSelection(null); return; }
        interactions.SetSelection(C_PREFIX + documentId.ToString("N") + "/" + elementId);
    }
    /// <summary>Reads a UI selection only when it belongs to the requested screen document.</summary>
    /// <param name="interactions">The native coordinator.</param>
    /// <param name="documentId">The requested document identity.</param>
    /// <param name="elementId">Receives the complete authored identity.</param>
    /// <returns>Whether a matching UI selection exists.</returns>
    public static bool TryRead(
        EditorInteractions interactions,
        Guid documentId,
        out string elementId
    ) {
        string prefix = C_PREFIX + documentId.ToString("N") + "/";
        if (interactions.selection.selectedTarget is string selected && selected.StartsWith(prefix, StringComparison.Ordinal) && selected.Length > prefix.Length)
        { elementId = selected[prefix.Length..]; return true; }
        elementId = string.Empty; return false;
    }
    /// <summary>Resolves shared or local focus against the current native tree without replacing another document's selection.</summary>
    /// <param name="interactions">The native coordinator.</param>
    /// <param name="documentId">The screen document identity.</param>
    /// <param name="screen">The current detached draft after any Undo/Redo.</param>
    /// <param name="fallback">The panel's previous local element identity.</param>
    /// <returns>A currently existing authored element identity.</returns>
    public static string Resolve(
        EditorInteractions interactions,
        Guid documentId,
        ScreenAsset screen,
        string fallback
    ) {
        bool shared = TryRead(interactions, documentId, out var selected);
        string candidate = shared ? selected : fallback;
        if (ScreenEditService.Flatten(screen).Any(row => row.element.id == candidate)) return candidate;
        if (shared) Select(interactions, documentId, screen.root.id);
        return screen.root.id;
    }
}

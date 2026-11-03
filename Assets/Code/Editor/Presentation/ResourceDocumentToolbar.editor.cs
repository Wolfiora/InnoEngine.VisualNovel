using System;
using System.Linq;
using InnoEngine.Assets;
using InnoEditor.Assets;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Editor.Localization;

namespace Inno.VisualNovel.Editor.Presentation;

internal sealed class ResourceDocumentToolbar(
    ResourceDocumentsModule documents,
    EditorInteractions interactions,
    Action finish
)
{
    internal Guid documentId { get; private set; }
    internal bool Draw<T>(
        string label,
        string extension,
        Func<T>? create,
        out T? draft
    ) where T : AssetObject
    {
        if (create is not null && ImGui.Button("Create" + label)) { finish(); documentId = documents.Create(create()).documentId; }
        if (create is not null) ImGui.SameLine();
        if (ImGui.Button("Open selected" + label))
        {
            finish();
            AssetPath? path = interactions.selection.selectedTarget switch
            {
                T asset => asset.assetPath,
                AssetFileEntry file when string.Equals(file.extension, extension, StringComparison.OrdinalIgnoreCase) => file.assetPath,
                _ => null
            };
            if (path is null) throw new InvalidOperationException("Select " + extension + " in File Browser.");
            documentId = documents.Open(path.Value).documentId;
        }
        if (documents.activeDocument is { } active && active.assetPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) documentId = active.documentId;
        var document = interactions.documents.documents.FirstOrDefault(item => item.documentId == documentId);
        draft = null;
        if (document is null) { EditorUiSession.current.widgets.Hint("Create a resource, or select an existing one and open it. Changes use the shared engine document and Undo/Redo history."); return false; }
        ImGui.SameLine(); if (ImGui.Button("Save")) { finish(); interactions.documents.Save(documentId); }
        ImGui.SameLine(); if (ImGui.Button("Undo")) { finish(); interactions.history.Undo(); }
        ImGui.SameLine(); if (ImGui.Button("Redo")) { finish(); interactions.history.Redo(); }
        ImGui.Text(document.title + (document.isDirty ? " · Unsaved" : " · Saved"));
        draft = documents.Read<T>(documentId); return true;
    }
}

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
    private readonly EditorTextView m_text = new(interactions);
    internal Guid documentId { get; private set; }
    internal bool Draw<T>(
        string label,
        string extension,
        Func<T>? create,
        out T? draft
    ) where T : AssetObject
    {
        if (create is not null && ImGui.Button(m_text.Caption("创建" + label, m_text.Localize("创建") + m_text.Localize(label)))) { finish(); documentId = documents.Create(create()).documentId; }
        if (create is not null) ImGui.SameLine();
        if (ImGui.Button(m_text.Caption("打开所选" + label, m_text.Localize("打开所选") + m_text.Localize(label))))
        {
            finish();
            AssetPath? path = interactions.selection.selectedTarget switch
            {
                T asset => asset.assetPath,
                AssetFileEntry file when string.Equals(file.extension, extension, StringComparison.OrdinalIgnoreCase) => file.assetPath,
                _ => null
            };
            if (path is null) throw new InvalidOperationException(m_text.Localize("在 File Browser 中选择 ") + extension + m_text.Localize(" 资源。"));
            documentId = documents.Open(path.Value).documentId;
        }
        if (documents.activeDocument is { } active && active.assetPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) documentId = active.documentId;
        var document = interactions.documents.documents.FirstOrDefault(item => item.documentId == documentId);
        draft = null;
        if (document is null) { EditorUiSession.current.widgets.Hint(m_text.Localize("创建资源，或选择已有资源后打开。操作共享引擎文档与 History。")); return false; }
        ImGui.SameLine(); if (ImGui.Button(m_text.Label("保存"))) { finish(); interactions.documents.Save(documentId); }
        ImGui.SameLine(); if (ImGui.Button(m_text.Label("撤销"))) { finish(); interactions.history.Undo(); }
        ImGui.SameLine(); if (ImGui.Button(m_text.Label("重做"))) { finish(); interactions.history.Redo(); }
        ImGui.Text(document.title + (document.isDirty ? m_text.Localize(" · 未保存") : m_text.Localize(" · 已保存")));
        draft = documents.Read<T>(documentId); return true;
    }
}

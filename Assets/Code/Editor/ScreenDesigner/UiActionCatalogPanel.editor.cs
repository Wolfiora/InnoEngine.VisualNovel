using System;
using System.Linq;
using System.Numerics;
using InnoEditor.Core;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Extensions;
using Inno.VisualNovel.Model.Interface.Actions;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Editor.ScreenDesigner;

/// <summary>Edits neutral UI action forms using the shared resource documents and native History.</summary>
[EditorPanel("inno.vn.ui-action-catalog", "VN Custom UI Actions", order: 465, defaultOpen: false, menuPath: "Authoring")]
public sealed class UiActionCatalogPanel : EditorPanel, IEditorPanelReloadState
{
    private readonly ResourceDocumentsModule m_documents;
    private readonly StoryInspectorFields m_fields;
    private readonly NovelValueView m_values;
    private readonly ResourceDocumentToolbar m_toolbar;
    private string m_error = string.Empty;
    /// <summary>Uses the current generation's shared authoring document and gesture owners.</summary>
    /// <param name="documents">The native resource document service.</param>
    /// <param name="interactions">The native History and selection service.</param>
    public UiActionCatalogPanel(
        ResourceDocumentsModule documents,
        EditorInteractions interactions
    ) { m_documents = documents; m_fields = new(interactions); m_values = new(m_fields); m_toolbar = new(documents, interactions, m_fields.Finish); }
    /// <inheritdoc />
    public override Vector2 initialSize => new(680, 720);
    /// <inheritdoc />
    protected override void OnDraw(EditorContext context)
    {
        m_fields.BeginFrame();
        try { Draw(); }
        catch (Exception error) when (error is not OutOfMemoryException) { m_error = (error).Message; }
        finally { m_fields.EndFrame(); }
        if (m_error.Length > 0) { EditorUiSession.current.widgets.Hint(m_error); if (ImGui.Button("Clear message")) m_error = string.Empty; }
    }
    /// <inheritdoc />
    public ReadOnlyMemory<byte> CaptureReloadState() { m_fields.Finish(); return m_documents.CaptureReload(); }
    /// <inheritdoc />
    public void RestoreReloadState(ReadOnlyMemory<byte> state) => m_documents.RestoreReload(state.Span);
    /// <inheritdoc />
    protected override void OnDetach(EditorContext context) => m_fields.Finish();
    private void Draw()
    {
        if (!m_toolbar.Draw<UiActionCatalogAsset>("Action catalog", ".ivnuiactions", static () => new(), out var catalog) || catalog is null) return;
        EditorUiSession.current.widgets.Hint("The action catalog stores typed forms. Register implementations in C#, link the catalog in game settings, then choose actions in Screen Designer. Only state written through existing bindings is saved.");
        if (ImGui.Button("Add action contract"))
        {
            m_fields.Finish(); m_documents.Edit<UiActionCatalogAsset>(m_toolbar.documentId, "Add UI action", candidate => UiActionCatalogEditService.Set(candidate, new() { id = "custom." + Guid.NewGuid().ToString("N"), title = "Custom action" }));
        }
        foreach (var original in catalog.definitions)
        {
            if (!ImGui.CollapsingHeader(original.title + " · " + original.id)) continue;
            var item = UiActionContract.Copy(original); string prefix = "action/" + original.id;
            string id = item.id, title = item.title, help = item.help, label = item.argument.label;
            bool changed = m_fields.Text(prefix + "/id", "Stable ID (starts with custom.)", ref id); item.id = id;
            changed |= m_fields.Text(prefix + "/title", "Display name", ref title); item.title = title;
            changed |= m_fields.MultilineText(prefix + "/help", "Action description", ref help); item.help = help;
            changed |= m_fields.Text(prefix + "/label", "Parameter name", ref label); item.argument.label = label;
            NovelValue value = item.argument.defaultValue;
            changed |= m_values.Draw(prefix + "/default", ref value, enumMembers: item.argument.members); item.argument.defaultValue = value;
            if (value.kind == NovelValueKind.Enum || value.kind == NovelValueKind.List && value.elementKind == NovelValueKind.Enum)
            {
                if (item.argument.members.Length == 0) item.argument.members = [value.kind == NovelValueKind.Enum ? value.text : "Default"];
                string members = string.Join(", ", item.argument.members);
                if (m_fields.Text(prefix + "/members", "Enum members (comma-separated)", ref members))
                { item.argument.members = members.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries); changed = true; }
            }
            bool preview = item.capabilities.HasFlag(CommandCapabilities.Preview), rollback = item.capabilities.HasFlag(CommandCapabilities.Rollback), external = item.capabilities.HasFlag(CommandCapabilities.ExternalEffects);
            changed |= m_fields.Toggle(prefix + "/preview", "Allow controlled preview", ref preview);
            changed |= m_fields.Toggle(prefix + "/rollback", "Allow rollback across this action", ref rollback);
            changed |= m_fields.Toggle(prefix + "/external", "External side effects (creates a barrier; disabled in preview)", ref external);
            item.capabilities = CommandCapabilities.Save | (preview ? CommandCapabilities.Preview : 0) | (rollback && !external ? CommandCapabilities.Rollback : 0) | (external ? CommandCapabilities.ExternalEffects : 0);
            if (changed) m_documents.Edit<UiActionCatalogAsset>(m_toolbar.documentId, "Edit UI action contract", candidate => UiActionCatalogEditService.Set(candidate, item, original.id), m_fields.mergeKey);
            if (ImGui.Button("Remove contract##" + prefix)) { m_fields.Finish(); m_documents.Edit<UiActionCatalogAsset>(m_toolbar.documentId, "Remove UI action contract", candidate => UiActionCatalogEditService.Remove(candidate, original.id)); }
        }
    }
}

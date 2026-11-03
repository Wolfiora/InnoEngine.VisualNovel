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
using Inno.VisualNovel.Model.Interface.Data;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Editor.ScreenDesigner;

/// <summary>Edits custom typed binding metadata through the same native document and History owners as pages.</summary>
[EditorPanel("inno.vn.ui-data-catalog", "VN Custom UI Data", order: 466, defaultOpen: false, menuPath: "Authoring")]
public sealed class UiDataSourceCatalogPanel : EditorPanel, IEditorPanelReloadState
{
    private readonly ResourceDocumentsModule m_documents;
    private readonly StoryInspectorFields m_fields;
    private readonly UiDataFieldsView m_fieldView;
    private readonly ResourceDocumentToolbar m_toolbar;
    private string m_error = string.Empty;
    /// <summary>Uses native document transactions and editor gestures for complete source schema editing.</summary>
    /// <param name="documents">The native resource document owner.</param>
    /// <param name="interactions">The current generation's History and selection service.</param>
    public UiDataSourceCatalogPanel(
        ResourceDocumentsModule documents,
        EditorInteractions interactions
    ) { m_documents = documents; m_fields = new(interactions); m_fieldView = new(m_fields); m_toolbar = new(documents, interactions, m_fields.Finish); }
    /// <inheritdoc />
    public override Vector2 initialSize => new(700, 720);
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
        if (!m_toolbar.Draw<UiDataSourceCatalogAsset>("UI data catalog", ".ivnuidata", static () => new(), out var catalog) || catalog is null) return;
        EditorUiSession.current.widgets.Hint("Declare field/list types and read/write permissions. Linking the catalog to a game adds binding menus in Screen Designer. C# implementations use the existing UiDataSource contract. Persistent state belongs in native variables or screen fields.");
        if (ImGui.Button("Add data source"))
        { m_fields.Finish(); m_documents.Edit<UiDataSourceCatalogAsset>(m_toolbar.documentId, "Add UI data source", candidate => UiDataSourceCatalogEditService.Set(candidate, new() { id = "s" + Guid.NewGuid().ToString("N"), title = "Data source" })); }
        foreach (var original in catalog.definitions)
        {
            if (!ImGui.CollapsingHeader(original.title + " · custom." + original.id)) continue;
            var item = UiDataSourceContract.Copy(original); string prefix = "source/" + original.id;
            string id = item.id, title = item.title, help = item.help;
            bool changed = m_fields.Text(prefix + "/id", "Source ID (single segment)", ref id); item.id = id;
            changed |= m_fields.Text(prefix + "/title", "Display name", ref title); item.title = title;
            changed |= m_fields.MultilineText(prefix + "/help", "Purpose and state description", ref help); item.help = help;
            bool preview = item.capabilities.HasFlag(CommandCapabilities.Preview), rollback = item.capabilities.HasFlag(CommandCapabilities.Rollback), external = item.capabilities.HasFlag(CommandCapabilities.ExternalEffects);
            changed |= m_fields.Toggle(prefix + "/preview", "Allow preview queries", ref preview);
            changed |= m_fields.Toggle(prefix + "/rollback", "Allow rollback across writes", ref rollback);
            changed |= m_fields.Toggle(prefix + "/external", "Depends on external side effects (disabled in preview)", ref external);
            item.capabilities = CommandCapabilities.Save | (preview ? CommandCapabilities.Preview : 0) | (rollback && !external ? CommandCapabilities.Rollback : 0) | (external ? CommandCapabilities.ExternalEffects : 0);
            var rootFields = item.fields;
            if (ImGui.CollapsingHeader("Root fields##" + prefix)) changed |= m_fieldView.Draw(prefix + "/root", ref rootFields); item.fields = rootFields;
            string images = string.Join(", ", item.images);
            if (m_fields.Text(prefix + "/images", "Image field IDs (comma separated)", ref images)) { item.images = images.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries); changed = true; }
            if (ImGui.Button("Add list##" + prefix))
            { m_fields.Finish(); item.collections = [.. item.collections, new() { id = "rows" + Guid.NewGuid().ToString("N"), title = "List", fields = [new() { schema = new() { id = "text", label = "Text", defaultValue = NovelValue.String("") } }] }]; changed = true; }
            string remove = string.Empty;
            foreach (var collection in item.collections)
            {
                if (!ImGui.CollapsingHeader("List · " + collection.title + "##" + prefix + "/" + collection.id)) continue;
                string collectionId = collection.id, collectionTitle = collection.title;
                changed |= m_fields.Text(prefix + "/" + collection.id + "/id", "List ID", ref collectionId); collection.id = collectionId;
                changed |= m_fields.Text(prefix + "/" + collection.id + "/title", "Display name", ref collectionTitle); collection.title = collectionTitle;
                var rowFields = collection.fields; changed |= m_fieldView.Draw(prefix + "/rows/" + collection.id, ref rowFields); collection.fields = rowFields;
                if (ImGui.Button("Delete list##" + prefix + "/" + collection.id)) { m_fields.Finish(); remove = collection.id; changed = true; }
            }
            if (remove.Length > 0) item.collections = item.collections.Where(row => row.id != remove).ToArray();
            if (changed) m_documents.Edit<UiDataSourceCatalogAsset>(m_toolbar.documentId, "Edit UI data contract", candidate => UiDataSourceCatalogEditService.Set(candidate, item, original.id), m_fields.mergeKey);
            if (ImGui.Button("Delete data source##" + prefix)) { m_fields.Finish(); m_documents.Edit<UiDataSourceCatalogAsset>(m_toolbar.documentId, "Delete UI data source", candidate => UiDataSourceCatalogEditService.Remove(candidate, original.id)); }
        }
    }
}

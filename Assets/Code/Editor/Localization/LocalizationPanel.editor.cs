using System;
using System.Linq;
using System.Numerics;
using InnoEditor.Core;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.LocalizationEditing;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Model.Project;

namespace Inno.VisualNovel.Editor.Localization;

/// <summary>Edits native game translations, source synchronization and reviewable CSV/TSV exchange.</summary>
[EditorPanel("inno.vn.localization", "VN Localization", order: 458, defaultOpen: false, menuPath: "Authoring")]
public sealed class LocalizationPanel : EditorPanel, IEditorPanelReloadState
{
    private readonly ResourceDocumentsModule m_documents;
    private readonly EditorInteractions m_interactions;
    private readonly StoryInspectorFields m_fields;
    private readonly ResourceDocumentToolbar m_toolbar;
    private readonly LanguageResourcesView m_resources;
    private readonly LanguageGlyphAuditView m_glyphAudit;
    private readonly LocalizationExchangeView m_exchange;
    private string m_language = "en", m_newLanguage = "en", m_newName = "English", m_selected = "", m_search = "", m_message = "";
    private bool m_missingOnly;
    private int m_page;
    /// <summary>Binds native translation drafts to engine selection and History.</summary>
    /// <param name="documents">The native resource owner.</param>
    /// <param name="interactions">The editor interaction coordinator.</param>
    public LocalizationPanel(
        ResourceDocumentsModule documents,
        EditorInteractions interactions
    ) {
        m_documents = documents; m_interactions = interactions; m_fields = new(interactions);
        m_toolbar = new(documents, interactions, m_fields.Finish);
        m_resources = new(documents, m_fields);
        m_glyphAudit = new(documents, m_fields);
        m_exchange = new(documents, m_fields);
    }
    /// <inheritdoc />
    public override Vector2 initialSize => new(1040, 820);
    /// <inheritdoc />
    protected override void OnDraw(EditorContext context)
    {
        m_fields.BeginFrame();
        try { m_glyphAudit.Update(); Draw(context.projectDirectory); }
        catch (Exception failure) when (failure is not OutOfMemoryException) { m_message = (failure).Message; }
        finally { m_fields.EndFrame(); }
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
    }
    /// <inheritdoc />
    public ReadOnlyMemory<byte> CaptureReloadState() { m_fields.Finish(); m_glyphAudit.Dispose(); m_exchange.Reset(); return m_documents.CaptureReload(); }
    /// <inheritdoc />
    public void RestoreReloadState(ReadOnlyMemory<byte> state) => m_documents.RestoreReload(state.Span);
    /// <inheritdoc />
    protected override void OnDetach(EditorContext context) { m_fields.Finish(); m_exchange.Reset(); m_glyphAudit.Dispose(); }
    private void Draw(string projectDirectory)
    {
        if (!m_toolbar.Draw("Translation table", ".ivnlocale", static () => new LocalizationTableAsset(), out LocalizationTableAsset? table) || table is null) return;
        m_fields.Text("locale/new-tag", "Language ID", ref m_newLanguage); m_fields.Text("locale/new-name", "Language name", ref m_newName);
        if (ImGui.Button("Add or rename language"))
        { m_fields.Finish(); m_documents.Edit<LocalizationTableAsset>(m_toolbar.documentId, "Configure game languages", candidate => LocalizationEditService.SetLanguage(candidate, m_newLanguage, m_newName)); }
        if (NovelResourceSelection.Get<NovelProjectAsset>(m_interactions) is { } game && ImGui.Button("Synchronize text from selected game: " + game.title))
        {
            m_fields.Finish(); var rows = LocalizationExtractionService.Extract(game);
            m_documents.Edit<LocalizationTableAsset>(m_toolbar.documentId, "Synchronize game source text", candidate => LocalizationEditService.Synchronize(candidate, rows));
        }
        if (!ImGui.BeginTabBar("locale-tabs")) return;
        try
        {
            if (ImGui.BeginTabItem("Translation and review")) { try { DrawEntries(table); } finally { ImGui.EndTabItem(); } }
            if (ImGui.BeginTabItem("Fonts and images")) { try { m_resources.Draw(table, m_toolbar.documentId, m_interactions); } finally { ImGui.EndTabItem(); } }
            if (ImGui.BeginTabItem("Glyph coverage")) { try { m_glyphAudit.Draw(table, m_interactions); } finally { ImGui.EndTabItem(); } }
            if (ImGui.BeginTabItem("CSV / TSV exchange")) { try { m_exchange.Draw(table, m_toolbar.documentId, projectDirectory); } finally { ImGui.EndTabItem(); } }
        }
        finally { ImGui.EndTabBar(); }
    }
    private void DrawEntries(LocalizationTableAsset table)
    {
        m_fields.Select("locale/language", "Target language", ref m_language, table.languages.Where(item => item.id != table.sourceLanguage).Select(static item => (item.id, item.name)));
        ImGui.InputText("Find ID / source text", ref m_search, 4096);
        m_fields.Toggle("locale/missing", "Show missing or outdated translations only", ref m_missingOnly);
        var rows = table.entries.Where(row => (m_search.Length == 0 || row.id.Contains(m_search, StringComparison.OrdinalIgnoreCase) || row.source.Contains(m_search, StringComparison.OrdinalIgnoreCase))
            && (!m_missingOnly || !LocalizationEditService.IsCurrent(row, m_language))).ToArray();
        m_page = Math.Clamp(m_page, 0, Math.Max(0, (rows.Length - 1) / 50));
        if (ImGui.Button("Previous page")) m_page = Math.Max(0, m_page - 1);
        ImGui.SameLine(); if (ImGui.Button("Next page")) m_page = Math.Min(Math.Max(0, (rows.Length - 1) / 50), m_page + 1);
        ImGui.Text(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0} entries · Page {1} · 50 per page", rows.Length, m_page + 1));
        foreach (var row in rows.Skip(m_page * 50).Take(50))
            if (ImGui.Selectable((LocalizationEditService.IsCurrent(row, m_language) ? "✓ " : "Needs review · ") + row.source.Replace('\n', ' ') + "##" + row.id, m_selected == row.id))
            { m_fields.Finish(); m_selected = row.id; }
        LocalizedText? selected = table.entries.SingleOrDefault(row => row.id == m_selected);
        if (selected is null) return;
        ImGui.SeparatorText(selected.id); EditorUiSession.current.widgets.Hint(selected.source);
        if (!table.languages.Any(item => item.id == m_language) || m_language == table.sourceLanguage) return;
        var current = selected.translations.SingleOrDefault(item => item.language == m_language);
        string text = current?.text ?? "", voice = current?.voiceCueId ?? "";
        TranslationStatus status = current?.status ?? TranslationStatus.Draft;
        bool changed = m_fields.MultilineText(selected.id + "/text", "Translation", ref text);
        changed |= m_fields.Text(selected.id + "/voice", "Voice cue ID for this language", ref voice);
        if (changed)
            m_documents.Edit<LocalizationTableAsset>(m_toolbar.documentId, "Edit translation draft", candidate => LocalizationEditService.SetTranslation(candidate, selected.id, m_language, text, TranslationStatus.Draft, voice), m_fields.mergeKey);
        if (m_fields.SelectEnum(selected.id + "/status", "Review status", ref status))
            m_documents.Edit<LocalizationTableAsset>(m_toolbar.documentId, "Review translation", candidate => LocalizationEditService.SetTranslation(candidate, selected.id, m_language, text, status, voice), m_fields.mergeKey);
        EditorUiSession.current.widgets.Hint("Editing text marks it as a draft. Reviewed translations are used at runtime. Source changes trigger fallback and another review. Variable placeholders must match the source.");
    }
}

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
[EditorPanel("inno.vn.localization", "VN 多语言 · Localization", order: 458, menuPath: "Authoring")]
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
        catch (Exception failure) when (failure is not OutOfMemoryException) { m_message = m_fields.Error(failure); }
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
        if (!m_toolbar.Draw("翻译表", ".ivnlocale", static () => new LocalizationTableAsset(), out LocalizationTableAsset? table) || table is null) return;
        m_fields.Text("locale/new-tag", m_fields.Localize("语言标识"), ref m_newLanguage); m_fields.Text("locale/new-name", m_fields.Localize("语言名称"), ref m_newName);
        if (ImGui.Button(m_fields.Label("添加或重命名语言")))
        { m_fields.Finish(); m_documents.Edit<LocalizationTableAsset>(m_toolbar.documentId, m_fields.Localize("配置游戏语言"), candidate => LocalizationEditService.SetLanguage(candidate, m_newLanguage, m_newName)); }
        if (NovelResourceSelection.Get<NovelProjectAsset>(m_interactions) is { } game && ImGui.Button(m_fields.Caption("同步所选游戏的文本：" + game.title, m_fields.Localize("同步所选游戏的文本：") + game.title)))
        {
            m_fields.Finish(); var rows = LocalizationExtractionService.Extract(game);
            m_documents.Edit<LocalizationTableAsset>(m_toolbar.documentId, m_fields.Localize("同步游戏原文"), candidate => LocalizationEditService.Synchronize(candidate, rows));
        }
        if (!ImGui.BeginTabBar("locale-tabs")) return;
        try
        {
            if (ImGui.BeginTabItem(m_fields.Label("翻译与复核"))) { try { DrawEntries(table); } finally { ImGui.EndTabItem(); } }
            if (ImGui.BeginTabItem(m_fields.Label("字体与图片"))) { try { m_resources.Draw(table, m_toolbar.documentId, m_interactions); } finally { ImGui.EndTabItem(); } }
            if (ImGui.BeginTabItem(m_fields.Label("缺字检查"))) { try { m_glyphAudit.Draw(table, m_interactions); } finally { ImGui.EndTabItem(); } }
            if (ImGui.BeginTabItem(m_fields.Label("CSV / TSV 交换"))) { try { m_exchange.Draw(table, m_toolbar.documentId, projectDirectory); } finally { ImGui.EndTabItem(); } }
        }
        finally { ImGui.EndTabBar(); }
    }
    private void DrawEntries(LocalizationTableAsset table)
    {
        m_fields.Select("locale/language", m_fields.Localize("目标语言"), ref m_language, table.languages.Where(item => item.id != table.sourceLanguage).Select(static item => (item.id, item.name)));
        ImGui.InputText(m_fields.Label("查找 ID / 原文"), ref m_search, 4096);
        m_fields.Toggle("locale/missing", "仅显示缺译或原文修改", ref m_missingOnly);
        var rows = table.entries.Where(row => (m_search.Length == 0 || row.id.Contains(m_search, StringComparison.OrdinalIgnoreCase) || row.source.Contains(m_search, StringComparison.OrdinalIgnoreCase))
            && (!m_missingOnly || !LocalizationEditService.IsCurrent(row, m_language))).ToArray();
        m_page = Math.Clamp(m_page, 0, Math.Max(0, (rows.Length - 1) / 50));
        if (ImGui.Button(m_fields.Label("上一页"))) m_page = Math.Max(0, m_page - 1);
        ImGui.SameLine(); if (ImGui.Button(m_fields.Label("下一页"))) m_page = Math.Min(Math.Max(0, (rows.Length - 1) / 50), m_page + 1);
        ImGui.Text(m_fields.Format("{0} 条 · 第 {1} 页 · 每页 50 条", rows.Length, m_page + 1));
        foreach (var row in rows.Skip(m_page * 50).Take(50))
            if (ImGui.Selectable(m_fields.Caption((LocalizationEditService.IsCurrent(row, m_language) ? "✓ " : "待复核 · ") + row.source.Replace('\n', ' ') + "##" + row.id, (LocalizationEditService.IsCurrent(row, m_language) ? "✓ " : m_fields.Localize("待复核 · ")) + row.source.Replace('\n', ' ') + "##" + row.id), m_selected == row.id))
            { m_fields.Finish(); m_selected = row.id; }
        LocalizedText? selected = table.entries.SingleOrDefault(row => row.id == m_selected);
        if (selected is null) return;
        ImGui.SeparatorText(selected.id); EditorUiSession.current.widgets.Hint(selected.source);
        if (!table.languages.Any(item => item.id == m_language) || m_language == table.sourceLanguage) return;
        var current = selected.translations.SingleOrDefault(item => item.language == m_language);
        string text = current?.text ?? "", voice = current?.voiceCueId ?? "";
        TranslationStatus status = current?.status ?? TranslationStatus.Draft;
        bool changed = m_fields.MultilineText(selected.id + "/text", m_fields.Localize("翻译"), ref text);
        changed |= m_fields.Text(selected.id + "/voice", m_fields.Localize("本语言语音提示 ID"), ref voice);
        if (changed)
            m_documents.Edit<LocalizationTableAsset>(m_toolbar.documentId, m_fields.Localize("编辑翻译草稿"), candidate => LocalizationEditService.SetTranslation(candidate, selected.id, m_language, text, TranslationStatus.Draft, voice), m_fields.mergeKey);
        if (m_fields.SelectEnum(selected.id + "/status", "复核状态", ref status))
            m_documents.Edit<LocalizationTableAsset>(m_toolbar.documentId, m_fields.Localize("复核翻译"), candidate => LocalizationEditService.SetTranslation(candidate, selected.id, m_language, text, status, voice), m_fields.mergeKey);
        EditorUiSession.current.widgets.Hint(m_fields.Localize("编辑文字会转为草稿。复核后运行时使用翻译；原文改变后自动回退并提示复核。变量占位符必须与原文一致。"));
    }
}

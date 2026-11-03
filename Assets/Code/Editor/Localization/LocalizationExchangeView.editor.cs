using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InnoEditor.ImGui;
using Inno.VisualNovel.Authoring.AssetImporting;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.LocalizationEditing;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Model.Localization;

namespace Inno.VisualNovel.Editor.Localization;

internal sealed class LocalizationExchangeView(
    ResourceDocumentsModule documents,
    StoryInspectorFields fields
)
{
    private Guid m_document;
    private string m_text = "", m_path = "", m_search = "", m_language = "", m_exported = "";
    private string[] m_headers = [];
    private readonly Dictionary<string, int> m_columns = new(StringComparer.Ordinal);
    private LocalizationImportPreview? m_preview;
    private bool m_tsv, m_issuesOnly;
    private int m_page, m_selected = -1;

    internal void Reset()
    {
        m_document = Guid.Empty; m_text = m_path = m_search = m_language = m_exported = "";
        m_headers = []; m_columns.Clear(); m_preview = null; m_page = 0; m_selected = -1;
    }
    internal void Draw(
        LocalizationTableAsset table,
        Guid document,
        string projectDirectory
    ) {
        if (m_document != document) { Reset(); m_document = document; }
        if (fields.Toggle("locale/tsv", "Use TSV (otherwise CSV)", ref m_tsv)) { fields.Finish(); MapHeaders(); }
        if (ImGui.Button("Export file and copy text"))
        {
            fields.Finish();
            m_exported = LocalizationExchangeFiles.Export(projectDirectory, table, document, m_tsv ? '\t' : ',');
            m_text = LocalizationDelimitedText.Export(table, m_tsv ? '\t' : ','); ImGui.SetClipboardText(m_text); MapHeaders();
        }
        if (m_exported.Length > 0) EditorUiSession.current.widgets.Hint(m_exported);
        EditorUiSession.current.widgets.Hint("Columns are id, source, language, translation, status, sourceFingerprint and voice. Quoted fields, commas and multiline Unicode text are supported.");
        ImGui.InputText("CSV / TSV file path", ref m_path, 32768);
        if (ImGui.Button("Read file")) { fields.Finish(); m_text = LocalizationExchangeFiles.Read(m_path); MapHeaders(); }
        if (m_headers.Length > 0)
        {
            ImGui.SeparatorText("Translation column mapping");
            EditorUiSession.current.widgets.Hint("Assign the seven fields to distinct columns. Choose custom or duplicate headers by position. Changing format or mappings invalidates the previous preview.");
            foreach (string column in LocalizationDelimitedText.columns)
            {
                string selected = m_columns[column].ToString(CultureInfo.InvariantCulture);
                if (fields.Select("locale/column/" + column, column, ref selected,
                    new[] { ("-1", "Unassigned") }.Concat(m_headers.Select((header, index) => (index.ToString(CultureInfo.InvariantCulture), (index + 1) + " · " + header)))))
                { m_columns[column] = int.Parse(selected, CultureInfo.InvariantCulture); m_preview = null; m_selected = -1; }
            }
        }
        ImGui.BeginDisabled(m_headers.Length == 0);
        try
        {
            if (ImGui.Button("Preview import"))
            { fields.Finish(); m_preview = LocalizationDelimitedText.Preview(table, m_text, m_tsv ? '\t' : ',', m_columns); m_page = 0; m_selected = -1; }
        }
        finally { ImGui.EndDisabled(); }
        if (m_preview is not { } preview) return;
        ImGui.Text(string.Format(System.Globalization.CultureInfo.InvariantCulture,"Ready to apply: {0} / {1} entries", preview.rows.Count(static row => row.applicable), preview.rows.Count));
        EditorUiSession.current.widgets.Hint("Only valid records are applied. Records with issues remain in the preview. Source conflicts preserve the current source, and outdated source revisions need another review.");
        if (ImGui.InputText("Find imported ID / source / translation", ref m_search, 4096)) m_page = 0;
        if (fields.Toggle("locale/import/issues", "Show import records with notices only", ref m_issuesOnly)) m_page = 0;
        if (fields.Select("locale/import/language", "Filter imported language", ref m_language,
            new[] { ("", "All languages") }.Concat(preview.rows.Select(static row => row.language).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(static id => (id, id))))) m_page = 0;
        var visible = preview.rows.Select((row, index) => (row, index)).Where(item => (!m_issuesOnly || item.row.message.Length > 0)
            && (m_language.Length == 0 || item.row.language == m_language)
            && (m_search.Length == 0 || item.row.id.Contains(m_search, StringComparison.OrdinalIgnoreCase) || item.row.source.Contains(m_search, StringComparison.OrdinalIgnoreCase)
                || item.row.text.Contains(m_search, StringComparison.OrdinalIgnoreCase))).ToArray();
        int pages = Math.Max(1, (visible.Length + 39) / 40); m_page = Math.Clamp(m_page, 0, pages - 1);
        if (ImGui.SmallButton("Previous page##locale-import")) m_page = Math.Max(0, m_page - 1);
        ImGui.SameLine(); if (ImGui.SmallButton("Next page##locale-import")) m_page = Math.Min(pages - 1, m_page + 1);
        ImGui.Text(string.Format(System.Globalization.CultureInfo.InvariantCulture,"Import review: {0} records · Page {1}/{2}", visible.Length, m_page + 1, pages));
        if (visible.Length == 0) EditorUiSession.current.widgets.Hint("No import records match the current filters.");
        foreach (var item in visible.Skip(m_page * 40).Take(40))
        {
            string state = item.row.applicable ? "Ready to apply" : "Blocked";
            string caption = state + " · " + item.row.id + " / " + item.row.language + " · " + Short(item.row.text, 100);
            if (ImGui.Selectable(caption + "###locale-import/" + document + "/" + item.index, m_selected == item.index)) m_selected = item.index;
            if (item.row.message.Length > 0) EditorUiSession.current.widgets.Hint(item.row.message);
        }
        if (m_selected >= 0 && m_selected < preview.rows.Count)
        {
            var selected = preview.rows[m_selected]; ImGui.SeparatorText(selected.id + " / " + selected.language);
            EditorUiSession.current.widgets.Hint("Source: " + Short(selected.source, 4000));
            EditorUiSession.current.widgets.Hint("Translation: " + Short(selected.text, 4000));
            ImGui.Text(selected.status + " · " + selected.fingerprint);
            if (selected.voice.Length > 0) EditorUiSession.current.widgets.Hint("Voice cue ID: " + selected.voice);
            if (selected.source.Length > 4000 || selected.text.Length > 4000) EditorUiSession.current.widgets.Hint("Long text is truncated in review details. Import preserves the complete content; proofread it in the source file.");
        }
        ImGui.BeginDisabled(!preview.rows.Any(static row => row.applicable));
        try
        {
            if (ImGui.Button("Apply valid translations from preview"))
            { fields.Finish(); documents.Edit<LocalizationTableAsset>(document, "Import translations", candidate => LocalizationDelimitedText.Apply(candidate, preview)); m_preview = null; }
        }
        finally { ImGui.EndDisabled(); }
    }
    private void MapHeaders()
    {
        m_preview = null; m_selected = -1; m_page = 0; m_columns.Clear(); m_headers = [];
        if (m_text.Length == 0) return;
        m_headers = DelimitedTextTable.Parse(m_text, m_tsv ? '\t' : ',').FirstOrDefault() ?? [];
        foreach (string column in LocalizationDelimitedText.columns)
            m_columns.Add(column, m_headers.Count(header => header.Equals(column, StringComparison.OrdinalIgnoreCase)) == 1
                ? Array.FindIndex(m_headers, header => header.Equals(column, StringComparison.OrdinalIgnoreCase)) : -1);
    }
    private static string Short(
        string text,
        int limit
    ) => text.Length > limit ? text[..limit] + "…" : text;
}

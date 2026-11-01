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
        if (fields.Toggle("locale/tsv", "使用 TSV（否则 CSV）", ref m_tsv)) { fields.Finish(); MapHeaders(); }
        if (ImGui.Button(fields.Label("导出文件并复制文本")))
        {
            fields.Finish();
            m_exported = LocalizationExchangeFiles.Export(projectDirectory, table, document, m_tsv ? '\t' : ',');
            m_text = LocalizationDelimitedText.Export(table, m_tsv ? '\t' : ','); ImGui.SetClipboardText(m_text); MapHeaders();
        }
        if (m_exported.Length > 0) EditorUiSession.current.widgets.Hint(m_exported);
        EditorUiSession.current.widgets.Hint(fields.Localize("列名为 id、source、language、translation、status、sourceFingerprint、voice。支持引号、逗号和多行 Unicode 文本。"));
        ImGui.InputText(fields.Label("CSV / TSV 文件路径"), ref m_path, 32768);
        if (ImGui.Button(fields.Label("读取文件"))) { fields.Finish(); m_text = LocalizationExchangeFiles.Read(m_path); MapHeaders(); }
        if (m_headers.Length > 0)
        {
            ImGui.SeparatorText(fields.Localize("翻译列映射"));
            EditorUiSession.current.widgets.Hint(fields.Localize("将七个字段分配到不同列；自定义或重复表头按位置选择。改变格式或映射会使旧预览失效。"));
            foreach (string column in LocalizationDelimitedText.columns)
            {
                string selected = m_columns[column].ToString(CultureInfo.InvariantCulture);
                if (fields.Select("locale/column/" + column, column, ref selected,
                    new[] { ("-1", fields.Localize("未分配")) }.Concat(m_headers.Select((header, index) => (index.ToString(CultureInfo.InvariantCulture), (index + 1) + " · " + header)))))
                { m_columns[column] = int.Parse(selected, CultureInfo.InvariantCulture); m_preview = null; m_selected = -1; }
            }
        }
        ImGui.BeginDisabled(m_headers.Length == 0);
        try
        {
            if (ImGui.Button(fields.Label("预览导入")))
            { fields.Finish(); m_preview = LocalizationDelimitedText.Preview(table, m_text, m_tsv ? '\t' : ',', m_columns); m_page = 0; m_selected = -1; }
        }
        finally { ImGui.EndDisabled(); }
        if (m_preview is not { } preview) return;
        ImGui.Text(fields.Format("可应用 {0} / {1} 条", preview.rows.Count(static row => row.applicable), preview.rows.Count));
        EditorUiSession.current.widgets.Hint(fields.Localize("仅应用有效记录。存在问题的记录保留在预览中；原文冲突不会覆盖当前原文，旧原文版本需要重新复核。"));
        if (ImGui.InputText(fields.Label("查找导入 ID / 原文 / 译文"), ref m_search, 4096)) m_page = 0;
        if (fields.Toggle("locale/import/issues", "仅显示有提示的导入记录", ref m_issuesOnly)) m_page = 0;
        if (fields.Select("locale/import/language", fields.Localize("导入语言筛选"), ref m_language,
            new[] { ("", fields.Localize("全部语言")) }.Concat(preview.rows.Select(static row => row.language).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(static id => (id, id))))) m_page = 0;
        var visible = preview.rows.Select((row, index) => (row, index)).Where(item => (!m_issuesOnly || item.row.message.Length > 0)
            && (m_language.Length == 0 || item.row.language == m_language)
            && (m_search.Length == 0 || item.row.id.Contains(m_search, StringComparison.OrdinalIgnoreCase) || item.row.source.Contains(m_search, StringComparison.OrdinalIgnoreCase)
                || item.row.text.Contains(m_search, StringComparison.OrdinalIgnoreCase))).ToArray();
        int pages = Math.Max(1, (visible.Length + 39) / 40); m_page = Math.Clamp(m_page, 0, pages - 1);
        if (ImGui.SmallButton(fields.Label("上一页##locale-import"))) m_page = Math.Max(0, m_page - 1);
        ImGui.SameLine(); if (ImGui.SmallButton(fields.Label("下一页##locale-import"))) m_page = Math.Min(pages - 1, m_page + 1);
        ImGui.Text(fields.Format("导入审核：{0} 条 · {1}/{2} 页", visible.Length, m_page + 1, pages));
        if (visible.Length == 0) EditorUiSession.current.widgets.Hint(fields.Localize("当前筛选没有导入记录。"));
        foreach (var item in visible.Skip(m_page * 40).Take(40))
        {
            string state = item.row.applicable ? fields.Localize("可应用") : fields.Localize("已阻止");
            string caption = state + " · " + item.row.id + " / " + item.row.language + " · " + Short(item.row.text, 100);
            if (ImGui.Selectable(caption + "###locale-import/" + document + "/" + item.index, m_selected == item.index)) m_selected = item.index;
            if (item.row.message.Length > 0) EditorUiSession.current.widgets.Hint(fields.Localize(item.row.message));
        }
        if (m_selected >= 0 && m_selected < preview.rows.Count)
        {
            var selected = preview.rows[m_selected]; ImGui.SeparatorText(selected.id + " / " + selected.language);
            EditorUiSession.current.widgets.Hint(fields.Localize("原文：") + Short(selected.source, 4000));
            EditorUiSession.current.widgets.Hint(fields.Localize("译文：") + Short(selected.text, 4000));
            ImGui.Text(selected.status + " · " + selected.fingerprint);
            if (selected.voice.Length > 0) EditorUiSession.current.widgets.Hint(fields.Localize("语音提示 ID：") + selected.voice);
            if (selected.source.Length > 4000 || selected.text.Length > 4000) EditorUiSession.current.widgets.Hint(fields.Localize("长文本在审核详情中截取显示；导入保留完整内容，请在源文件中校对全文。"));
        }
        ImGui.BeginDisabled(!preview.rows.Any(static row => row.applicable));
        try
        {
            if (ImGui.Button(fields.Label("应用预览中的有效翻译")))
            { fields.Finish(); documents.Edit<LocalizationTableAsset>(document, fields.Localize("导入翻译"), candidate => LocalizationDelimitedText.Apply(candidate, preview)); m_preview = null; }
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

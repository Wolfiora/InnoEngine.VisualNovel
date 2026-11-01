using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InnoEngine.Assets;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.AssetImporting;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Project;

namespace Inno.VisualNovel.Editor.Workspace;

internal sealed class StoryImportView(
    StoryInspectorFields fields,
    EditorInteractions interactions
)
{
    private string m_path = "", m_text = "", m_format = "csv", m_name = "导入对白", m_entry = "imported";
    private AssetPath? m_game;
    private string[] m_headers = [];
    private readonly Dictionary<string, int> m_columns = new(StringComparer.Ordinal);
    private StoryImportPreview? m_review;
    private int m_page;
    private Guid m_document;
    internal void Draw(
        StoryDocumentsModule documents,
        StoryDraftState draft,
        string projectDirectory,
        Action<string> select
    ) {
        if (m_document != documents.activeDocumentId) { fields.Finish(); m_document = documents.activeDocumentId; m_review = null; }
        EditorUiSession.current.widgets.Hint(fields.Localize("导入为新片段及公开入口，保留原有分支和连线。先审核匹配，全部记录有效后作为一次原生 History 操作导入。图片、演出与选项继续在图/剧本中编辑。"));
        if (fields.Select("story-import/format", fields.Localize("源格式"), ref m_format, [("csv", fields.Localize("CSV 表格")), ("tsv", fields.Localize("TSV 表格")), ("text", fields.Localize("纯文本段落"))])) { m_review = null; MapHeaders(); }
        ImGui.InputText(fields.Label("剧本文件路径"), ref m_path, 32768);
        if (ImGui.Button(fields.Label("读取剧本文件"))) { fields.Finish(); m_text = StoryExchangeFiles.Read(m_path); m_review = null; MapHeaders(); }
        if (NovelResourceSelection.Get<NovelProjectAsset>(interactions) is { } selected && ImGui.Button(fields.Caption("匹配所选游戏角色与语音：" + selected.title, fields.Localize("匹配所选游戏角色与语音：") + selected.title)))
        { m_game = selected.assetPath; m_review = null; }
        NovelProjectAsset? game = m_game is { } path ? Assets.Load<NovelProjectAsset>(path) : null;
        EditorUiSession.current.widgets.Hint(game is null ? fields.Localize("未指定游戏：可导入旁白和字面姓名；角色 ID 与语音需要先选择对应游戏。") : fields.Localize("资源匹配上下文：") + game.title);
        if (m_format != "text" && m_headers.Length > 0)
        {
            foreach (string column in StoryImportService.columns)
            {
                string index = m_columns[column].ToString(CultureInfo.InvariantCulture);
                if (fields.Select("story-import/column/" + column, column, ref index,
                    new[] { ("-1", fields.Localize("不分配")) }.Concat(m_headers.Select((header, position) => (position.ToString(CultureInfo.InvariantCulture), (position + 1) + " · " + header)))))
                { m_columns[column] = int.Parse(index, CultureInfo.InvariantCulture); m_review = null; }
            }
        }
        if (ImGui.Button(fields.Label("预览角色、语音与文本 ID 匹配")))
        {
            fields.Finish(); m_page = 0;
            m_review = m_format == "text" ? documents.imports.PreviewText(draft, m_text, game?.characters, game?.audio)
                : documents.imports.PreviewDelimited(draft, m_text, m_format == "tsv" ? '\t' : ',', m_columns, game?.characters, game?.audio);
        }
        if (m_review is { } review)
        {
            int pages = Math.Max(1, (review.rows.Count + 39) / 40); m_page = Math.Clamp(m_page, 0, pages - 1);
            ImGui.Text(fields.Format("{0} 条 · 错误 {1} · {2}/{3}", review.rows.Count, review.rows.Count(static row => row.message.Length > 0), m_page + 1, pages));
            if (ImGui.Button(fields.Label("上一页##import"))) m_page = Math.Max(0, m_page - 1); ImGui.SameLine(); if (ImGui.Button(fields.Label("下一页##import"))) m_page = Math.Min(pages - 1, m_page + 1);
            foreach (var row in review.rows.Skip(m_page * 40).Take(40)) EditorUiSession.current.widgets.Hint(
                fields.Localize("记录 ") + row.record + " · " + (row.character.Length > 0 ? row.character : row.speaker.Length > 0 ? row.speaker : fields.Localize("旁白")) + " · " + row.presentation
                + "\n" + (row.text.Length > 300 ? row.text[..300] + "…" : row.text) + fields.Localize("\n文本 ID：") + row.textId + (row.voice.Length > 0 ? fields.Localize(" · 语音：") + row.voice : "") + (row.message.Length > 0 ? fields.Localize("\n错误：") + row.message : ""));
            fields.Text("story-import/name", fields.Localize("新片段名称"), ref m_name); fields.Text("story-import/entry", fields.Localize("公开入口 ID"), ref m_entry);
            ImGui.BeginDisabled(review.rows.Any(static row => row.message.Length > 0));
            try
            {
                if (ImGui.Button(fields.Label("将全部已审核对白导入新片段")))
                {
                    fields.Finish(); string node = "";
                    documents.Edit(documents.activeDocumentId, fields.Localize("导入对白片段"), candidate => node = documents.imports.Append(candidate, review, m_name, m_entry, game?.characters, game?.audio));
                    select(node); m_review = null;
                }
            }
            finally { ImGui.EndDisabled(); }
        }
        ImGui.SeparatorText(fields.Localize("阅读稿导出"));
        if (ImGui.Button(fields.Label("导出当前章节阅读稿")))
        { fields.Finish(); string exported = StoryExchangeFiles.ExportReading(projectDirectory, draft, documents.codec, documents.activeDocumentId, game?.characters); EditorUiSession.current.widgets.Hint(exported); }
    }
    private void MapHeaders()
    {
        m_headers = m_format == "text" || m_text.Length == 0 ? [] : DelimitedTextTable.Parse(m_text, m_format == "tsv" ? '\t' : ',').FirstOrDefault() ?? [];
        m_columns.Clear(); foreach (string column in StoryImportService.columns) m_columns.Add(column, Array.FindIndex(m_headers, header => header.Equals(column, StringComparison.OrdinalIgnoreCase)));
    }
}

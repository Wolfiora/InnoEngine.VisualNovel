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
    private string m_path = "", m_text = "", m_format = "csv", m_name = "Import dialogue", m_entry = "imported";
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
        EditorUiSession.current.widgets.Hint("Import as a new fragment and public entry, preserving existing branches and connections. Review matches first; all records must be valid to import as one native History operation. Edit images, performances and choices in graph/script.");
        if (fields.Select("story-import/format", "Source format", ref m_format, [("csv", "CSV table"), ("tsv", "TSV table"), ("text", "Plain-text paragraphs")])) { m_review = null; MapHeaders(); }
        ImGui.InputText("Script file path", ref m_path, 32768);
        if (ImGui.Button("Read script file")) { fields.Finish(); m_text = StoryExchangeFiles.Read(m_path); m_review = null; MapHeaders(); }
        if (NovelResourceSelection.Get<NovelProjectAsset>(interactions) is { } selected && ImGui.Button("Match characters and voices from selected game: " + selected.title))
        { m_game = selected.assetPath; m_review = null; }
        NovelProjectAsset? game = m_game is { } path ? Assets.Load<NovelProjectAsset>(path) : null;
        EditorUiSession.current.widgets.Hint(game is null ? "No game specified: narration and literal names can be imported; character IDs and voices require selecting the game first." : "Resource matching context: " + game.title);
        if (m_format != "text" && m_headers.Length > 0)
        {
            foreach (string column in StoryImportService.columns)
            {
                string index = m_columns[column].ToString(CultureInfo.InvariantCulture);
                if (fields.Select("story-import/column/" + column, column, ref index,
                    new[] { ("-1", "Unassigned") }.Concat(m_headers.Select((header, position) => (position.ToString(CultureInfo.InvariantCulture), (position + 1) + " · " + header)))))
                { m_columns[column] = int.Parse(index, CultureInfo.InvariantCulture); m_review = null; }
            }
        }
        if (ImGui.Button("Preview character, voice and text ID matches"))
        {
            fields.Finish(); m_page = 0;
            m_review = m_format == "text" ? documents.imports.PreviewText(draft, m_text, game?.characters, game?.audio)
                : documents.imports.PreviewDelimited(draft, m_text, m_format == "tsv" ? '\t' : ',', m_columns, game?.characters, game?.audio);
        }
        if (m_review is { } review)
        {
            int pages = Math.Max(1, (review.rows.Count + 39) / 40); m_page = Math.Clamp(m_page, 0, pages - 1);
            ImGui.Text(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0} records · {1} errors · {2}/{3}", review.rows.Count, review.rows.Count(static row => row.message.Length > 0), m_page + 1, pages));
            if (ImGui.Button("Previous##import")) m_page = Math.Max(0, m_page - 1); ImGui.SameLine(); if (ImGui.Button("Next##import")) m_page = Math.Min(pages - 1, m_page + 1);
            foreach (var row in review.rows.Skip(m_page * 40).Take(40)) EditorUiSession.current.widgets.Hint(
                "Record " + row.record + " · " + (row.character.Length > 0 ? row.character : row.speaker.Length > 0 ? row.speaker : "Narration") + " · " + row.presentation
                + "\n" + (row.text.Length > 300 ? row.text[..300] + "…" : row.text) + "\nText ID: " + row.textId + (row.voice.Length > 0 ? " · Voice: " + row.voice : "") + (row.message.Length > 0 ? "\nError: " + row.message : ""));
            fields.Text("story-import/name", "New fragment name", ref m_name); fields.Text("story-import/entry", "Public entry ID", ref m_entry);
            ImGui.BeginDisabled(review.rows.Any(static row => row.message.Length > 0));
            try
            {
                if (ImGui.Button("Import all reviewed dialogue as new fragment"))
                {
                    fields.Finish(); string node = "";
                    documents.Edit(documents.activeDocumentId, "Import dialogue fragment", candidate => node = documents.imports.Append(candidate, review, m_name, m_entry, game?.characters, game?.audio));
                    select(node); m_review = null;
                }
            }
            finally { ImGui.EndDisabled(); }
        }
        ImGui.SeparatorText("Proofread export");
        if (ImGui.Button("Export current chapter for proofreading"))
        { fields.Finish(); string exported = StoryExchangeFiles.ExportReading(projectDirectory, draft, documents.codec, documents.activeDocumentId, game?.characters); EditorUiSession.current.widgets.Hint(exported); }
    }
    private void MapHeaders()
    {
        m_headers = m_format == "text" || m_text.Length == 0 ? [] : DelimitedTextTable.Parse(m_text, m_format == "tsv" ? '\t' : ',').FirstOrDefault() ?? [];
        m_columns.Clear(); foreach (string column in StoryImportService.columns) m_columns.Add(column, Array.FindIndex(m_headers, header => header.Equals(column, StringComparison.OrdinalIgnoreCase)));
    }
}

using System;
using System.Globalization;
using System.Linq;
using System.Text;
using InnoEditor.Core;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.LocalizationEditing;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Model.Project;

namespace Inno.VisualNovel.Editor.Localization;

internal sealed class LanguageGlyphAuditView(
    ResourceDocumentsModule documents,
    StoryInspectorFields fields
) : IDisposable
{
    private GlyphCoverageAudit? m_job;
    private string m_language = "zh-CN", m_game = string.Empty;
    private int m_page;
    internal void Update() => m_job?.Step(2);
    internal void Draw(
        LocalizationTableAsset table,
        EditorInteractions interactions
    ) {
        fields.Select("locale/glyph/language", "Language to check", ref m_language, table.languages.Select(static item => (item.id, item.name)));
        EditorUiSession.current.widgets.Hint("Select a game to check its saved compiled chapters, themes and screens using the current translation draft and actual font context. Static checks cannot prove coverage for variable values, player input or extension-generated text. If native shaping is unavailable, the result is a check failure rather than a claim that no glyphs are missing.");
        if (ImGui.Button("Check selected game's static text"))
        {
            fields.Finish(); var game = NovelResourceSelection.Get<NovelProjectAsset>(interactions) ?? throw new InvalidOperationException("Select a saved game definition in File Browser.");
            var candidate = NovelProjectCopy.Copy(game); candidate.localization = table;
            var job = new GlyphCoverageAudit(GlyphCoverageSources.Enumerate(candidate, m_language)); m_job?.Dispose(); m_job = job; m_game = game.title; m_page = 0;
        }
        if (m_job is null) return;
        if (!m_job.completed && !m_job.cancelled && ImGui.Button("Cancel glyph coverage check")) m_job.Cancel();
        ImGui.Text(m_game + " · " + (m_job.cancelled ? "Cancelled" : m_job.completed ? "Finished" : "Checking") + " · Contexts: " + m_job.processed + " · Issues: " + m_job.issues.Count);
        ImGui.Text("Contexts containing dynamic variables · " + m_job.dynamicSources);
        if (m_job.truncated) EditorUiSession.current.widgets.Hint("The 10,000-issue limit was reached; results are incomplete. Fix the primary font configuration and check again.");
        int pages = Math.Max(1, (m_job.issues.Count + 39) / 40); m_page = Math.Clamp(m_page, 0, pages - 1);
        if (ImGui.Button("Previous page##glyph")) m_page = Math.Max(0, m_page - 1);
        ImGui.SameLine(); ImGui.Text((m_page + 1) + " / " + pages); ImGui.SameLine();
        if (ImGui.Button("Next page##glyph")) m_page = Math.Min(pages - 1, m_page + 1);
        foreach (var (issue, index) in m_job.issues.Skip(m_page * 40).Take(40).Select((issue, index) => (issue, index)))
        {
            ImGui.Text((issue.failed ? "Check failed · " : "Missing glyph · " + Codepoints(issue.text) + " × " + issue.count + " · ") + issue.location);
            EditorUiSession.current.widgets.Hint(issue.message + " Text ID: " + issue.textId);
            if (issue.assetPath.localPath.Length == 0) continue;
            if (ImGui.SmallButton((issue.nodeId.Length > 0 ? "Locate chapter node" : "Open screen asset") + "##glyph/" + index))
            {
                fields.Finish();
                if (issue.nodeId.Length > 0)
                {
                    if (!interactions.TryGetModule<StoryDocumentsModule>(out var stories) || stories is null) throw new InvalidOperationException("The chapter document service is unavailable.");
                    var opened = stories.Open(issue.assetPath); StoryNodeSelection.Select(interactions, opened.documentId, issue.nodeId);
                }
                else
                {
                    var opened = documents.Open(issue.assetPath);
                    if (issue.elementId.Length > 0)
                    {
                        var screen = documents.Read<Model.Interface.ScreenAsset>(opened.documentId);
                        if (!ScreenEditService.Flatten(screen).Any(row => row.element.id == issue.elementId)) throw new InvalidOperationException("The original control changed or was removed. Check again.");
                        ScreenElementSelection.Select(interactions, opened.documentId, issue.elementId);
                    }
                }
            }
        }
    }
    /// <inheritdoc />
    public void Dispose() { m_job?.Dispose(); m_job = null; }
    private static string Codepoints(string text)
    {
        var result = new StringBuilder();
        foreach (var rune in text.EnumerateRunes()) { if (result.Length > 0) result.Append(' '); result.Append("U+").Append(rune.Value.ToString("X4", CultureInfo.InvariantCulture)); }
        return result.ToString();
    }
}

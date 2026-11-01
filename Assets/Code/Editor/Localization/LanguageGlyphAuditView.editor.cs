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
        fields.Select("locale/glyph/language", fields.Localize("检查语言"), ref m_language, table.languages.Select(static item => (item.id, item.name)));
        EditorUiSession.current.widgets.Hint(fields.Localize("选择游戏后检查其已保存编译章节、主题和页面，使用当前翻译表草稿及实际字体上下文。变量值、玩家输入和扩展动态文本无法静态证明覆盖。原生塑形不可用会显示“检查失败”，不会冒充没有缺字。"));
        if (ImGui.Button(fields.Label("检查所选游戏的静态文字")))
        {
            fields.Finish(); var game = NovelResourceSelection.Get<NovelProjectAsset>(interactions) ?? throw new InvalidOperationException(fields.Localize("在 File Browser 选择已保存的游戏定义。"));
            var candidate = NovelProjectCopy.Copy(game); candidate.localization = table;
            var job = new GlyphCoverageAudit(GlyphCoverageSources.Enumerate(candidate, m_language)); m_job?.Dispose(); m_job = job; m_game = game.title; m_page = 0;
        }
        if (m_job is null) return;
        if (!m_job.completed && !m_job.cancelled && ImGui.Button(fields.Label("取消缺字检查"))) m_job.Cancel();
        ImGui.Text(m_game + " · " + (m_job.cancelled ? fields.Localize("已取消") : m_job.completed ? fields.Localize("已结束") : fields.Localize("检查中")) + fields.Localize(" · 上下文 ") + m_job.processed + fields.Localize(" · 问题 ") + m_job.issues.Count);
        ImGui.Text(fields.Localize("含动态变量的上下文 · ") + m_job.dynamicSources);
        if (m_job.truncated) EditorUiSession.current.widgets.Hint(fields.Localize("问题达到 10000 条，结果不完整。修复主要字体配置后重新检查。"));
        int pages = Math.Max(1, (m_job.issues.Count + 39) / 40); m_page = Math.Clamp(m_page, 0, pages - 1);
        if (ImGui.Button(fields.Label("上一页##glyph"))) m_page = Math.Max(0, m_page - 1);
        ImGui.SameLine(); ImGui.Text((m_page + 1) + " / " + pages); ImGui.SameLine();
        if (ImGui.Button(fields.Label("下一页##glyph"))) m_page = Math.Min(pages - 1, m_page + 1);
        foreach (var (issue, index) in m_job.issues.Skip(m_page * 40).Take(40).Select((issue, index) => (issue, index)))
        {
            ImGui.Text((issue.failed ? fields.Localize("检查失败 · ") : fields.Localize("缺字 · ") + Codepoints(issue.text) + " × " + issue.count + " · ") + issue.location);
            EditorUiSession.current.widgets.Hint(issue.message + fields.Localize(" 文本 ID：") + issue.textId);
            if (issue.assetPath.localPath.Length == 0) continue;
            if (ImGui.SmallButton(fields.Caption((issue.nodeId.Length > 0 ? "定位章节节点" : "打开页面资源") + "##glyph/" + index, (issue.nodeId.Length > 0 ? fields.Localize("定位章节节点") : fields.Localize("打开页面资源")) + "##glyph/" + index)))
            {
                fields.Finish();
                if (issue.nodeId.Length > 0)
                {
                    if (!interactions.TryGetModule<StoryDocumentsModule>(out var stories) || stories is null) throw new InvalidOperationException(fields.Localize("章节文档服务不可用。"));
                    var opened = stories.Open(issue.assetPath); StoryNodeSelection.Select(interactions, opened.documentId, issue.nodeId);
                }
                else
                {
                    var opened = documents.Open(issue.assetPath);
                    if (issue.elementId.Length > 0)
                    {
                        var screen = documents.Read<Model.Interface.ScreenAsset>(opened.documentId);
                        if (!ScreenEditService.Flatten(screen).Any(row => row.element.id == issue.elementId)) throw new InvalidOperationException(fields.Localize("原控件已改变或删除，请重新检查。"));
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

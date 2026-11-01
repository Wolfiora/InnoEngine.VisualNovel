using System;
using System.Linq;
using System.Numerics;
using InnoEngine.Assets;
using InnoEditor.Core;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.VoiceManagement;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Editor.Localization;

namespace Inno.VisualNovel.Editor.Audio;

/// <summary>Reviews stable line matches, exports language-specific recording sheets and applies native voice batches.</summary>
[EditorPanel("inno.vn.voices", "VN 配音管理 · Voice Manager", order: 467, menuPath: "Authoring")]
public sealed class VoiceManagementPanel : EditorPanel
{
    private readonly EditorTextView m_text;
    private readonly StoryDocumentsModule m_stories;
    private readonly ResourceDocumentsModule m_resources;
    private readonly EditorInteractions m_interactions;
    private readonly VoiceBatchService m_service;
    private readonly VoiceBatchImportService m_import;
    private Guid m_game;
    private Guid m_chapter;
    private Guid m_locale, m_localeAsset;
    private string m_language = "";
    private string m_folder = "Game/Voices", m_prefix = "", m_message = "";
    private bool m_replace, m_tsv;
    private int m_page;
    private VoiceBatchPreview? m_preview;
    /// <summary>Binds both native document owners and the engine transaction coordinator.</summary>
    /// <param name="stories">The authoritative graph and script documents.</param>
    /// <param name="resources">The native game resource documents.</param>
    /// <param name="interactions">The shared editor History and selection owner.</param>
    public VoiceManagementPanel(
        StoryDocumentsModule stories,
        ResourceDocumentsModule resources,
        EditorInteractions interactions
    ) { m_stories = stories; m_resources = resources; m_interactions = interactions; m_service = new(stories.codec); m_import = new(stories, resources, interactions); m_text = new(interactions); }
    /// <inheritdoc />
    public override Vector2 initialSize => new(1040, 780);
    /// <inheritdoc />
    protected override void OnDraw(EditorContext context)
    {
        try { Draw(context.projectDirectory); }
        catch (Exception error) when (error is not OutOfMemoryException) { m_message = m_text.Error(error); }
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
    }
    private void Draw(string projectDirectory)
    {
        EditorUiSession.current.widgets.Hint(m_text.Localize("选择游戏定义、章节和配音语言。录音文件名使用稳定台词 ID，可添加统一前缀；匹配不会模糊猜测。目标译文需要先审核，语言版本语音写入翻译表。"));
        if (ImGui.Button(m_text.Label("使用所选游戏")))
        {
            var game = NovelResourceSelection.Get<NovelProjectAsset>(m_interactions) ?? throw new InvalidOperationException(m_text.Localize("请选择 .ivnproject。"));
            m_game = m_resources.Open(game.assetPath).documentId; m_preview = null; m_locale = m_localeAsset = Guid.Empty; m_language = "";
        }
        ImGui.SameLine();
        if (ImGui.Button(m_text.Label("使用所选章节")))
        {
            var chapter = NovelResourceSelection.Get<StoryAsset>(m_interactions) ?? throw new InvalidOperationException(m_text.Localize("请选择 .ivnstory。"));
            m_chapter = m_stories.Open(chapter.assetPath).documentId; m_preview = null;
        }
        if (m_game == Guid.Empty || m_chapter == Guid.Empty) return;
        var project = m_resources.Read<NovelProjectAsset>(m_game); var draft = m_stories.Read(m_chapter);
        if (project.localization is { } locale && locale.assetPath.source == AssetSourceId.project && locale.assetPath.localPath.Length > 0)
        {
            if (m_localeAsset != locale.identity.persistentId)
            { m_locale = m_resources.Open(locale.assetPath).documentId; m_localeAsset = locale.identity.persistentId; m_preview = null; }
            project.localization = m_resources.Read<LocalizationTableAsset>(m_locale);
        }
        ImGui.Text(project.title + " · " + draft.title);
        var languages = project.localization?.languages ?? [new LanguageDefinition { id = project.defaultLanguage, name = project.defaultLanguage }];
        string sourceLanguage = project.localization?.sourceLanguage ?? project.defaultLanguage;
        if (!languages.Any(item => item.id == m_language)) { m_language = sourceLanguage; m_preview = null; }
        var widgets = EditorUiSession.current.widgets;
        if (widgets.BeginBoundedCombo(m_text.Label("配音语言"), languages.FirstOrDefault(item => item.id == m_language)?.name ?? m_language))
        {
            try { foreach (var language in languages) if (ImGui.Selectable(language.name + "##" + language.id, language.id == m_language)) { m_language = language.id; m_preview = null; } }
            finally { widgets.EndBoundedCombo(); }
        }
        if (ImGui.InputText(m_text.Label("录音文件夹（Assets 内）"), ref m_folder, 4096)) m_preview = null;
        if (ImGui.InputText(m_text.Label("文件名前缀（可空）"), ref m_prefix, 1024)) m_preview = null;
        if (ImGui.Checkbox(m_text.Label("替换已关联的语音"), ref m_replace)) m_preview = null;
        ImGui.Checkbox(m_text.Label("导出 TSV（否则 CSV）"), ref m_tsv);
        if (ImGui.Button(m_text.Label("预览录音匹配")))
        { m_preview = m_service.Preview(draft, project, NativeVoiceImports.ReadFolder(projectDirectory, m_folder), m_prefix, m_replace, m_language); m_page = 0; }
        ImGui.SameLine();
        if (ImGui.Button(m_text.Label("导出配音稿/回填清单")))
            m_message = m_text.Localize("已导出：") + NativeVoiceImports.Export(projectDirectory, m_service.Export(draft,
                m_preview ?? m_service.Preview(draft, project, [], m_prefix, m_replace, m_language), m_tsv ? '\t' : ',', project.localization), m_tsv ? '\t' : ',');
        if (ImGui.Button(m_text.Label("保存章节、翻译与游戏")))
        {
            if (!m_interactions.documents.Save(m_chapter) || m_locale != Guid.Empty && !m_interactions.documents.Save(m_locale)
                || !m_interactions.documents.Save(m_game)) throw new InvalidOperationException(m_text.Localize("保存未全部完成。已成功写入的源保留，请修复引擎诊断并重试保存。"));
            m_message = m_text.Localize("章节与游戏资源库已保存。");
        }
        if (m_preview is null) return;
        ImGui.Text(m_text.Localize("可关联 ") + m_preview.rows.Count(static item => item.applicable) + " / " + m_preview.rows.Count + m_text.Localize(" 条。"));
        if (m_preview.rows.Any(static item => item.applicable) && ImGui.Button(m_text.Label("应用审核后的有效匹配")))
        {
            int count = m_import.Apply(m_chapter, m_game, m_preview); m_preview = null;
            m_message = m_text.Localize("已关联 ") + count + m_text.Localize(" 句，章节/翻译表与游戏可一起撤销。保存相关文档后再运行；新建的音频提示源保持为独立资源。"); return;
        }
        int pages = Math.Max(1, (m_preview.rows.Count + 49) / 50); m_page = Math.Clamp(m_page, 0, pages - 1);
        if (ImGui.Button(m_text.Label("上一页"))) m_page = Math.Max(0, m_page - 1); ImGui.SameLine(); if (ImGui.Button(m_text.Label("下一页"))) m_page = Math.Min(pages - 1, m_page + 1);
        ImGui.Text(m_text.Format("第 {0}/{1} 页", m_page + 1, pages));
        foreach (var row in m_preview.rows.Skip(m_page * 50).Take(50))
        {
            if (ImGui.Selectable(row.textId + " · " + row.speaker + " · " + row.message + "##" + row.node)) StoryNodeSelection.Select(m_interactions, m_chapter, row.node);
            EditorUiSession.current.widgets.Hint(row.text + (row.clipPath.Length > 0 ? "\n" + row.clipPath : ""));
        }
    }
}

using System;
using System.Linq;
using System.Numerics;
using InnoEngine.Assets;
using InnoEditor.Core;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.ProjectEditing;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Project;

namespace Inno.VisualNovel.Editor.Onboarding;

/// <summary>Connects authored resources and public story entries to an existing playable game.</summary>
[EditorPanel("inno.vn.project", "VN 游戏设置 · Game Settings", order: 454, menuPath: "Authoring")]
public sealed class NovelProjectPanel : EditorPanel, IEditorPanelReloadState
{
    private readonly ResourceDocumentsModule m_documents;
    private readonly EditorInteractions m_interactions;
    private readonly ResourceDocumentToolbar m_toolbar;
    private readonly StoryInspectorFields m_fields;
    private string m_screenRole = "reading";
    private string m_customRole = "custom.page";
    private string m_message = string.Empty;
    /// <summary>Binds game resources to engine-owned documents and History.</summary>
    /// <param name="documents">The native resource draft owner.</param>
    /// <param name="interactions">The host selection and History coordinator.</param>
    public NovelProjectPanel(
        ResourceDocumentsModule documents,
        EditorInteractions interactions
    ) {
        m_documents = documents; m_interactions = interactions; m_fields = new(interactions);
        m_toolbar = new(documents, interactions, m_fields.Finish);
    }
    /// <inheritdoc />
    public override Vector2 initialSize => new(760, 680);
    /// <inheritdoc />
    protected override void OnDraw(EditorContext context)
    {
        m_fields.BeginFrame();
        try { Draw(); }
        catch (Exception error) when (error is not OutOfMemoryException) { m_message = m_fields.Error(error); }
        finally { m_fields.EndFrame(); }
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
    }
    /// <inheritdoc />
    public ReadOnlyMemory<byte> CaptureReloadState() { m_fields.Finish(); return m_documents.CaptureReload(); }
    /// <inheritdoc />
    public void RestoreReloadState(ReadOnlyMemory<byte> state) => m_documents.RestoreReload(state.Span);
    /// <inheritdoc />
    protected override void OnDetach(EditorContext context) => m_fields.Finish();
    private void Draw()
    {
        if (!m_toolbar.Draw<NovelProjectAsset>("游戏定义", ".ivnproject", null, out var project) || project is null) return;
        bool changed = false;
        string title = project.title, game = project.gameId, chapter = project.startChapter, entry = project.startEntry, initial = project.initialStage;
        changed |= m_fields.Text("game/title", m_fields.Localize("游戏标题"), ref title); project.title = title;
        changed |= m_fields.Text("game/id", m_fields.Localize("稳定游戏 ID"), ref game); project.gameId = game;
        string language = project.defaultLanguage;
        changed |= m_fields.Select("game/language", m_fields.Localize("默认游戏语言"), ref language,
            project.localization?.languages.Select(static item => (item.id, item.name)) ?? [("zh-CN", m_fields.Localize("简体中文"))]); project.defaultLanguage = language;
        changed |= m_fields.Select("game/chapter", m_fields.Localize("起始章节"), ref chapter, project.chapters.Select(static item => (item.chapterId, item.title))); project.startChapter = chapter;
        changed |= m_fields.Select("game/entry", m_fields.Localize("公开入口"), ref entry, project.chapters.FirstOrDefault(item => item.chapterId == chapter)?.program?.entries.Select(static item => (item.id, item.id)) ?? []); project.startEntry = entry;
        changed |= m_fields.Select("game/stage", m_fields.Localize("初始舞台"), ref initial, new[] { (string.Empty, m_fields.Localize("空舞台")) }.Concat(project.stages.Select(static item => (item.resourceId, item.title)))); project.initialStage = initial;
        string theme = project.defaultTheme;
        changed |= m_fields.Select("game/theme", m_fields.Localize("默认主题"), ref theme, new[] { (string.Empty, m_fields.Localize("页面自身样式")) }.Concat(project.themes.Select(static item => (item.resourceId, item.title)))); project.defaultTheme = theme;
        double width = project.width, height = project.height, rollback = project.rollbackLimit;
        changed |= m_fields.Number("game/width", m_fields.Localize("设计宽度"), ref width, 320, 16384); project.width = (float)width;
        changed |= m_fields.Number("game/height", m_fields.Localize("设计高度"), ref height, 240, 16384); project.height = (float)height;
        changed |= m_fields.Number("game/rollback", m_fields.Localize("回滚检查点上限"), ref rollback, 1, 10000); project.rollbackLimit = (int)rollback;
        bool automatic = project.autoSaveOnChoices;
        changed |= m_fields.Toggle("game/auto-save-choices", "进入选项时自动存档", ref automatic); project.autoSaveOnChoices = automatic;
        double manualSlots = project.manualSaveSlots, automaticSlots = project.automaticSaveSlots;
        changed |= m_fields.Number("game/manual-slots", m_fields.Localize("手动存档槽位数量"), ref manualSlots, 1, 10000); project.manualSaveSlots = (int)manualSlots;
        changed |= m_fields.Number("game/automatic-slots", m_fields.Localize("自动存档轮换数量"), ref automaticSlots, 1, 100); project.automaticSaveSlots = (int)automaticSlots;
        m_fields.Select("game/screen-role", m_fields.Localize("附加页面角色"), ref m_screenRole, new[] { "loading", "title", "reading", "pause", "settings", "history", "credits", "save", "load", "confirm", "extras", "gallery", "music", "replays", "endings", "glossary", "characters", "collectionDetail" }.Select(static role => (role, role)));
        if (ImGui.Button(m_fields.Label("添加或替换所选资源")))
        {
            m_fields.Finish();
            AssetObject selected = NovelResourceSelection.Get<AssetObject>(m_interactions) ?? throw new InvalidOperationException(m_fields.Localize("在 File Browser 选择章节、舞台、角色、音频、页面、主题、字体、材质、命令目录、UI 动作或数据源目录。"));
            m_documents.Edit<NovelProjectAsset>(m_toolbar.documentId, m_fields.Localize("关联游戏资源"), candidate => NovelProjectEditService.Attach(candidate, selected, m_screenRole));
        }
        m_fields.Text("game/custom-page-role", m_fields.Localize("自定义页面角色"), ref m_customRole);
        if (ImGui.Button(m_fields.Label("将所选页面关联为自定义页面")))
        {
            m_fields.Finish(); var screen = NovelResourceSelection.Get<Model.Interface.ScreenAsset>(m_interactions) ?? throw new InvalidOperationException(m_fields.Localize("先选择已保存的 .ivnui 页面。"));
            m_documents.Edit<NovelProjectAsset>(m_toolbar.documentId, m_fields.Localize("关联自定义页面"), candidate => NovelProjectEditService.Attach(candidate, screen, m_customRole));
        }
        if (changed) m_documents.Edit<NovelProjectAsset>(m_toolbar.documentId, m_fields.Localize("编辑游戏设置"), candidate =>
        { candidate.title = project.title; candidate.gameId = project.gameId; candidate.defaultLanguage = project.defaultLanguage; candidate.startChapter = project.startChapter; candidate.startEntry = project.startEntry;
            candidate.initialStage = project.initialStage; candidate.defaultTheme = project.defaultTheme; candidate.width = project.width; candidate.height = project.height; candidate.rollbackLimit = project.rollbackLimit; candidate.autoSaveOnChoices = project.autoSaveOnChoices;
            candidate.manualSaveSlots = project.manualSaveSlots; candidate.automaticSaveSlots = project.automaticSaveSlots; }, m_fields.mergeKey);
        ImGui.Separator();
        if (ImGui.Button(m_fields.Label("将所选资产加入动态打包集合")))
        {
            m_fields.Finish();
            AssetObject selected = NovelResourceSelection.Get<AssetObject>(m_interactions) ?? throw new InvalidOperationException(m_fields.Localize("先在 File Browser 选择要显式打包的资源。"));
            m_documents.Edit<NovelProjectAsset>(m_toolbar.documentId, m_fields.Localize("添加动态打包资源"), candidate => NovelProjectEditService.AddPackagingRoot(candidate, selected));
        }
        foreach (AssetObject resource in project.additionalResources)
        {
            ImGui.Text(m_fields.Localize("附加打包：") + resource.assetPath);
            ImGui.SameLine();
            if (ImGui.SmallButton(m_fields.Caption("移除##pack-" + resource.identity.persistentId, m_fields.Localize("移除##pack-") + resource.identity.persistentId)))
            {
                m_fields.Finish();
                m_documents.Edit<NovelProjectAsset>(m_toolbar.documentId, m_fields.Localize("移除动态打包资源"), candidate => candidate.additionalResources = candidate.additionalResources.Where(item => item.identity.persistentId != resource.identity.persistentId).ToArray());
            }
        }
        ImGui.Text(m_fields.Localize("章节：") + string.Join("、", project.chapters.Select(static item => item.title)));
        ImGui.Text(m_fields.Localize("舞台：") + string.Join("、", project.stages.Select(static item => item.resourceId)));
        ImGui.Text(m_fields.Localize("角色：") + string.Join("、", project.characters.Select(static item => item.resourceId)));
        ImGui.Text(m_fields.Localize("音频：") + string.Join("、", project.audio.Select(static item => item.resourceId)));
        ImGui.Text(m_fields.Localize("标题音乐：") + (project.titleAudio?.name ?? m_fields.Localize("未分配")));
        ImGui.Text(m_fields.Localize("粒子：") + string.Join("、", project.particles.Select(static item => item.resourceId)));
        ImGui.Text(m_fields.Localize("转场：") + string.Join("、", project.transitions.Select(static item => item.resourceId)));
        ImGui.Text(m_fields.Localize("屏幕效果：") + string.Join("、", project.screenEffects.Select(static item => item.resourceId)));
        ImGui.Text(m_fields.Localize("图像处理材质：") + (project.effectsMaterial?.name ?? m_fields.Localize("未分配")));
        ImGui.Text(m_fields.Localize("翻译表：") + (project.localization?.title ?? m_fields.Localize("未分配")));
        ImGui.Text(m_fields.Localize("收藏库：") + (project.collections?.title ?? m_fields.Localize("未分配")));
        foreach (var action in project.uiActions)
        {
            ImGui.Text(m_fields.Localize("UI 动作 ") + action.title + " · " + action.id); ImGui.SameLine();
            if (ImGui.SmallButton(m_fields.Caption("移除##action-" + action.id, m_fields.Localize("移除##action-") + action.id)))
            { m_fields.Finish(); m_documents.Edit<NovelProjectAsset>(m_toolbar.documentId, m_fields.Localize("移除 UI 动作契约"), candidate => candidate.uiActions = candidate.uiActions.Where(item => item.id != action.id).ToArray()); }
        }
        foreach (var source in project.uiDataSources)
        {
            ImGui.Text(m_fields.Localize("UI 数据源 ") + source.title + " · " + source.id); ImGui.SameLine();
            if (ImGui.SmallButton(m_fields.Caption("移除##source-" + source.id, m_fields.Localize("移除##source-") + source.id)))
            { m_fields.Finish(); m_documents.Edit<NovelProjectAsset>(m_toolbar.documentId, m_fields.Localize("移除 UI 数据源契约"), candidate => candidate.uiDataSources = candidate.uiDataSources.Where(item => item.id != source.id).ToArray()); }
        }
        if (project.titleAudio is not null && ImGui.Button(m_fields.Label("移除标题音乐")))
        { m_fields.Finish(); m_documents.Edit<NovelProjectAsset>(m_toolbar.documentId, m_fields.Localize("移除标题音乐"), static candidate => candidate.titleAudio = null); }
        foreach (var page in project.screens) ImGui.Text(m_fields.Localize("页面 ") + page.role + " → " + page.screen?.name);
        foreach (var item in project.themes)
        {
            ImGui.Text(m_fields.Localize("主题 ") + item.resourceId + " · " + item.title); ImGui.SameLine();
            if (ImGui.SmallButton(m_fields.Caption("移除##theme-" + item.resourceId, m_fields.Localize("移除##theme-") + item.resourceId)))
            {
                m_fields.Finish(); m_documents.Edit<NovelProjectAsset>(m_toolbar.documentId, m_fields.Localize("移除主题"), candidate =>
                { candidate.themes = candidate.themes.Where(theme => theme.resourceId != item.resourceId).ToArray(); if (candidate.defaultTheme == item.resourceId) candidate.defaultTheme = string.Empty; });
            }
        }
    }
}

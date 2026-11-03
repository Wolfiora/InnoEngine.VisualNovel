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
[EditorPanel("inno.vn.project", "VN Game Settings", order: 454, defaultOpen: false, menuPath: "Authoring")]
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
        catch (Exception error) when (error is not OutOfMemoryException) { m_message = (error).Message; }
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
        if (!m_toolbar.Draw<NovelProjectAsset>("Game definition", ".ivnproject", null, out var project) || project is null) return;
        bool changed = false;
        string title = project.title, game = project.gameId, chapter = project.startChapter, entry = project.startEntry, initial = project.initialStage;
        changed |= m_fields.Text("game/title", "Game title", ref title); project.title = title;
        changed |= m_fields.Text("game/id", "Stable game ID", ref game); project.gameId = game;
        string language = project.defaultLanguage;
        changed |= m_fields.Select("game/language", "Default game language", ref language,
            project.localization?.languages.Select(static item => (item.id, item.name)) ?? [("zh-CN", "Simplified Chinese")]); project.defaultLanguage = language;
        changed |= m_fields.Select("game/chapter", "Starting chapter", ref chapter, project.chapters.Select(static item => (item.chapterId, item.title))); project.startChapter = chapter;
        changed |= m_fields.Select("game/entry", "Public entry", ref entry, project.chapters.FirstOrDefault(item => item.chapterId == chapter)?.program?.entries.Select(static item => (item.id, item.id)) ?? []); project.startEntry = entry;
        changed |= m_fields.Select("game/stage", "Initial stage", ref initial, new[] { (string.Empty, "Empty stage") }.Concat(project.stages.Select(static item => (item.resourceId, item.title)))); project.initialStage = initial;
        string theme = project.defaultTheme;
        changed |= m_fields.Select("game/theme", "Default theme", ref theme, new[] { (string.Empty, "Screen's own styling") }.Concat(project.themes.Select(static item => (item.resourceId, item.title)))); project.defaultTheme = theme;
        double width = project.width, height = project.height, rollback = project.rollbackLimit;
        changed |= m_fields.Number("game/width", "Design width", ref width, 320, 16384); project.width = (float)width;
        changed |= m_fields.Number("game/height", "Design height", ref height, 240, 16384); project.height = (float)height;
        changed |= m_fields.Number("game/rollback", "Maximum rollback checkpoints", ref rollback, 1, 10000); project.rollbackLimit = (int)rollback;
        bool automatic = project.autoSaveOnChoices;
        changed |= m_fields.Toggle("game/auto-save-choices", "Automatically save when choices appear", ref automatic); project.autoSaveOnChoices = automatic;
        double manualSlots = project.manualSaveSlots, automaticSlots = project.automaticSaveSlots;
        changed |= m_fields.Number("game/manual-slots", "Manual save slot count", ref manualSlots, 1, 10000); project.manualSaveSlots = (int)manualSlots;
        changed |= m_fields.Number("game/automatic-slots", "Rotating automatic save count", ref automaticSlots, 1, 100); project.automaticSaveSlots = (int)automaticSlots;
        m_fields.Select("game/screen-role", "Additional screen role", ref m_screenRole, new[] { "loading", "title", "reading", "pause", "settings", "history", "credits", "save", "load", "confirm", "extras", "gallery", "music", "replays", "endings", "glossary", "characters", "collectionDetail" }.Select(static role => (role, role)));
        if (ImGui.Button("Add or replace selected resource"))
        {
            m_fields.Finish();
            AssetObject selected = NovelResourceSelection.Get<AssetObject>(m_interactions) ?? throw new InvalidOperationException("Select a chapter, stage, character, audio cue, screen, theme, font, material, command catalog, UI action catalog or data source catalog in File Browser.");
            m_documents.Edit<NovelProjectAsset>(m_toolbar.documentId, "Link game resource", candidate => NovelProjectEditService.Attach(candidate, selected, m_screenRole));
        }
        m_fields.Text("game/custom-page-role", "Custom screen role", ref m_customRole);
        if (ImGui.Button("Link selected screen as a custom screen"))
        {
            m_fields.Finish(); var screen = NovelResourceSelection.Get<Model.Interface.ScreenAsset>(m_interactions) ?? throw new InvalidOperationException("Select a saved .ivnui screen first.");
            m_documents.Edit<NovelProjectAsset>(m_toolbar.documentId, "Link custom screen", candidate => NovelProjectEditService.Attach(candidate, screen, m_customRole));
        }
        if (changed) m_documents.Edit<NovelProjectAsset>(m_toolbar.documentId, "Edit game settings", candidate =>
        { candidate.title = project.title; candidate.gameId = project.gameId; candidate.defaultLanguage = project.defaultLanguage; candidate.startChapter = project.startChapter; candidate.startEntry = project.startEntry;
            candidate.initialStage = project.initialStage; candidate.defaultTheme = project.defaultTheme; candidate.width = project.width; candidate.height = project.height; candidate.rollbackLimit = project.rollbackLimit; candidate.autoSaveOnChoices = project.autoSaveOnChoices;
            candidate.manualSaveSlots = project.manualSaveSlots; candidate.automaticSaveSlots = project.automaticSaveSlots; }, m_fields.mergeKey);
        ImGui.Separator();
        if (ImGui.Button("Add selected asset to explicit packaging collection"))
        {
            m_fields.Finish();
            AssetObject selected = NovelResourceSelection.Get<AssetObject>(m_interactions) ?? throw new InvalidOperationException("Select the resource to package explicitly in File Browser first.");
            m_documents.Edit<NovelProjectAsset>(m_toolbar.documentId, "Add explicit packaging resource", candidate => NovelProjectEditService.AddPackagingRoot(candidate, selected));
        }
        foreach (AssetObject resource in project.additionalResources)
        {
            ImGui.Text("Additional packaging: " + resource.assetPath);
            ImGui.SameLine();
            if (ImGui.SmallButton("Remove##pack-" + resource.identity.persistentId))
            {
                m_fields.Finish();
                m_documents.Edit<NovelProjectAsset>(m_toolbar.documentId, "Remove explicit packaging resource", candidate => candidate.additionalResources = candidate.additionalResources.Where(item => item.identity.persistentId != resource.identity.persistentId).ToArray());
            }
        }
        ImGui.Text("Chapters: " + string.Join("、", project.chapters.Select(static item => item.title)));
        ImGui.Text("Stages: " + string.Join("、", project.stages.Select(static item => item.resourceId)));
        ImGui.Text("Characters: " + string.Join("、", project.characters.Select(static item => item.resourceId)));
        ImGui.Text("Audio: " + string.Join("、", project.audio.Select(static item => item.resourceId)));
        ImGui.Text("Title music: " + (project.titleAudio?.name ?? "Unassigned"));
        ImGui.Text("Particles: " + string.Join("、", project.particles.Select(static item => item.resourceId)));
        ImGui.Text("Transitions: " + string.Join("、", project.transitions.Select(static item => item.resourceId)));
        ImGui.Text("Screen effects: " + string.Join("、", project.screenEffects.Select(static item => item.resourceId)));
        ImGui.Text("Image processing material: " + (project.effectsMaterial?.name ?? "Unassigned"));
        ImGui.Text("Translation table: " + (project.localization?.title ?? "Unassigned"));
        ImGui.Text("Collection library: " + (project.collections?.title ?? "Unassigned"));
        foreach (var action in project.uiActions)
        {
            ImGui.Text("UI action " + action.title + " · " + action.id); ImGui.SameLine();
            if (ImGui.SmallButton("Remove##action-" + action.id))
            { m_fields.Finish(); m_documents.Edit<NovelProjectAsset>(m_toolbar.documentId, "Remove UI action contract", candidate => candidate.uiActions = candidate.uiActions.Where(item => item.id != action.id).ToArray()); }
        }
        foreach (var source in project.uiDataSources)
        {
            ImGui.Text("UI data source " + source.title + " · " + source.id); ImGui.SameLine();
            if (ImGui.SmallButton("Remove##source-" + source.id))
            { m_fields.Finish(); m_documents.Edit<NovelProjectAsset>(m_toolbar.documentId, "Remove UI data source contract", candidate => candidate.uiDataSources = candidate.uiDataSources.Where(item => item.id != source.id).ToArray()); }
        }
        if (project.titleAudio is not null && ImGui.Button("Remove title music"))
        { m_fields.Finish(); m_documents.Edit<NovelProjectAsset>(m_toolbar.documentId, "Remove title music", static candidate => candidate.titleAudio = null); }
        foreach (var page in project.screens) ImGui.Text("Screen " + page.role + " → " + page.screen?.name);
        foreach (var item in project.themes)
        {
            ImGui.Text("Theme " + item.resourceId + " · " + item.title); ImGui.SameLine();
            if (ImGui.SmallButton("Remove##theme-" + item.resourceId))
            {
                m_fields.Finish(); m_documents.Edit<NovelProjectAsset>(m_toolbar.documentId, "Remove theme", candidate =>
                { candidate.themes = candidate.themes.Where(theme => theme.resourceId != item.resourceId).ToArray(); if (candidate.defaultTheme == item.resourceId) candidate.defaultTheme = string.Empty; });
            }
        }
    }
}

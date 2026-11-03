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
[EditorPanel("inno.vn.voices", "VN Voice Manager", order: 467, defaultOpen: false, menuPath: "Authoring")]
public sealed class VoiceManagementPanel : EditorPanel
{
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
    ) { m_stories = stories; m_resources = resources; m_interactions = interactions; m_service = new(stories.codec); m_import = new(stories, resources, interactions);  }
    /// <inheritdoc />
    public override Vector2 initialSize => new(1040, 780);
    /// <inheritdoc />
    protected override void OnDraw(EditorContext context)
    {
        try { Draw(context.projectDirectory); }
        catch (Exception error) when (error is not OutOfMemoryException) { m_message = (error).Message; }
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
    }
    private void Draw(string projectDirectory)
    {
        EditorUiSession.current.widgets.Hint("Choose a game, chapter and voice language. Name recordings with stable dialogue IDs, optionally with a common prefix. Matching uses exact identities. Review translated lines before linking their recordings; localized voice bindings are stored in the translation table.");
        if (ImGui.Button("Use selected game"))
        {
            var game = NovelResourceSelection.Get<NovelProjectAsset>(m_interactions) ?? throw new InvalidOperationException("Select an .ivnproject asset.");
            m_game = m_resources.Open(game.assetPath).documentId; m_preview = null; m_locale = m_localeAsset = Guid.Empty; m_language = "";
        }
        ImGui.SameLine();
        if (ImGui.Button("Use selected chapter"))
        {
            var chapter = NovelResourceSelection.Get<StoryAsset>(m_interactions) ?? throw new InvalidOperationException("Select an .ivnstory asset.");
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
        if (widgets.BeginBoundedCombo("Voice language", languages.FirstOrDefault(item => item.id == m_language)?.name ?? m_language))
        {
            try { foreach (var language in languages) if (ImGui.Selectable(language.name + "##" + language.id, language.id == m_language)) { m_language = language.id; m_preview = null; } }
            finally { widgets.EndBoundedCombo(); }
        }
        if (ImGui.InputText("Recording folder (inside Assets)", ref m_folder, 4096)) m_preview = null;
        if (ImGui.InputText("Filename prefix (optional)", ref m_prefix, 1024)) m_preview = null;
        if (ImGui.Checkbox("Replace existing voice bindings", ref m_replace)) m_preview = null;
        ImGui.Checkbox("Export TSV (otherwise CSV)", ref m_tsv);
        if (ImGui.Button("Preview recording matches"))
        { m_preview = m_service.Preview(draft, project, NativeVoiceImports.ReadFolder(projectDirectory, m_folder), m_prefix, m_replace, m_language); m_page = 0; }
        ImGui.SameLine();
        if (ImGui.Button("Export voice script / recording manifest"))
            m_message = "Exported: " + NativeVoiceImports.Export(projectDirectory, m_service.Export(draft,
                m_preview ?? m_service.Preview(draft, project, [], m_prefix, m_replace, m_language), m_tsv ? '\t' : ',', project.localization), m_tsv ? '\t' : ',');
        if (ImGui.Button("Save chapter, translations and game"))
        {
            if (!m_interactions.documents.Save(m_chapter) || m_locale != Guid.Empty && !m_interactions.documents.Save(m_locale)
                || !m_interactions.documents.Save(m_game)) throw new InvalidOperationException("Some assets could not be saved. Successfully saved sources are retained. Resolve the engine diagnostics and save again.");
            m_message = "Chapter and game resources saved.";
        }
        if (m_preview is null) return;
        ImGui.Text("Ready to link: " + m_preview.rows.Count(static item => item.applicable) + " / " + m_preview.rows.Count + " recordings.");
        if (m_preview.rows.Any(static item => item.applicable) && ImGui.Button("Apply reviewed valid matches"))
        {
            int count = m_import.Apply(m_chapter, m_game, m_preview); m_preview = null;
            m_message = "Linked: " + count + " lines. Chapter, translation and game changes can be undone together. Save the affected documents before running. Newly created audio cues remain independent assets."; return;
        }
        int pages = Math.Max(1, (m_preview.rows.Count + 49) / 50); m_page = Math.Clamp(m_page, 0, pages - 1);
        if (ImGui.Button("Previous page")) m_page = Math.Max(0, m_page - 1); ImGui.SameLine(); if (ImGui.Button("Next page")) m_page = Math.Min(pages - 1, m_page + 1);
        ImGui.Text(string.Format(System.Globalization.CultureInfo.InvariantCulture,"Page {0} of {1}", m_page + 1, pages));
        foreach (var row in m_preview.rows.Skip(m_page * 50).Take(50))
        {
            if (ImGui.Selectable(row.textId + " · " + row.speaker + " · " + row.message + "##" + row.node)) StoryNodeSelection.Select(m_interactions, m_chapter, row.node);
            EditorUiSession.current.widgets.Hint(row.text + (row.clipPath.Length > 0 ? "\n" + row.clipPath : ""));
        }
    }
}

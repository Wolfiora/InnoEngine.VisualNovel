using System;
using System.Linq;
using InnoEngine.Scene;
using InnoEditor.ImGui;
using InnoEditor.Inspection;
using InnoEditor.Interactions;
using InnoEditor.Scene;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Integration.Hosting;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Editor.Localization;

namespace Inno.VisualNovel.Editor.Onboarding;

internal sealed class NovelGameCreationView(
    StoryDocumentsModule documents,
    EditorInteractions interactions,
    SceneEdits sceneEdits,
    IEditorSceneWorkspace scenes,
    IInspectionPropertyEditService properties
)
{
    private string m_title = "My First Story";
    private string m_theme = "Clear";
    private NovelTemplateKind m_template = NovelTemplateKind.Branching;
    private string m_sample = string.Empty;
    private string m_language = "zh-CN";
    private (float width, float height) m_resolution = (1920, 1080);
    private string m_result = string.Empty;

    internal void Draw()
    {
        EditorUiSession.current.widgets.Hint("Create a playable game with editable screens, a branching chapter and a startup scene. Choose Play, then use the Game view.");
        EditorUiSession.current.widgets.Hint("This SDK records scene creation as separate native Undo/Redo steps; grouped structural Redo is unavailable.");
        ImGui.InputText("Game title", ref m_title, 256);
        var widgets = EditorUiSession.current.widgets;
        string TemplateName(NovelTemplateKind kind) => kind switch { NovelTemplateKind.Blank => "Blank visual novel", NovelTemplateKind.Branching => "Short branching story", _ => "Presentation and interface showcase" };
        if (widgets.BeginBoundedCombo("Project template / tutorial", m_sample.Length == 0 ? TemplateName(m_template) : TutorialSampleCatalog.entries.Single(sample => sample.id == m_sample).title))
        {
            try
            {
                foreach (var kind in Enum.GetValues<NovelTemplateKind>()) if (ImGui.Selectable(TemplateName(kind), m_sample.Length == 0 && kind == m_template)) { m_template = kind; m_sample = string.Empty; }
                foreach (var sample in TutorialSampleCatalog.entries) if (ImGui.Selectable("Tutorial · " + sample.title, m_sample == sample.id)) m_sample = sample.id;
            }
            finally { widgets.EndBoundedCombo(); }
        }
        if (m_sample.Length > 0) widgets.Hint(TutorialSampleCatalog.entries.Single(sample => sample.id == m_sample).description);
        if (widgets.BeginBoundedCombo("Game language", m_language == "zh-CN" ? "Simplified Chinese" : "English"))
        {
            try { if (ImGui.Selectable("Simplified Chinese", m_language == "zh-CN")) m_language = "zh-CN"; if (ImGui.Selectable("English", m_language == "en")) m_language = "en"; }
            finally { widgets.EndBoundedCombo(); }
        }
        if (widgets.BeginBoundedCombo("Design resolution", m_resolution.width + " × " + m_resolution.height))
        {
            try { foreach (var size in new (float width, float height)[] { (1280, 720), (1920, 1080), (2560, 1440), (1920, 1200) })
                if (ImGui.Selectable(size.width + " × " + size.height, m_resolution == size)) m_resolution = size; }
            finally { widgets.EndBoundedCombo(); }
        }
        if (widgets.BeginBoundedCombo("Theme", m_theme))
        {
            try
            {
                foreach (string name in new[] { "Clear", "Paper", "Cinema" })
                    if (ImGui.Selectable(name, m_theme == name)) m_theme = name;
            }
            finally { widgets.EndBoundedCombo(); }
        }
        ImGui.BeginDisabled(!scenes.canPersist);
        try
        {
            if (ImGui.Button("Create visual novel"))
            {
                var service = new NovelGameTemplateService(documents.codec);
                NovelProjectAsset game = m_sample.Length == 0 ? service.Create(m_title, m_theme, m_template, m_language, m_resolution.width, m_resolution.height)
                    : service.CreateSample(m_sample, m_title, m_theme, m_language, m_resolution.width, m_resolution.height);
                GameScene scene = new Inno.VisualNovel.Authoring.SceneEditing.NovelSceneEditService(sceneEdits, properties).CreateGameScene(game);
                string directory = game.assetPath.localPath[..game.assetPath.localPath.LastIndexOf('/')];
                string scenePath = scenes.Save(scene, directory);
                m_result = "Created: " + game.assetPath + "; startup scene: " + scenePath;
            }
        }
        finally { ImGui.EndDisabled(); }
        if (m_result.Length > 0) widgets.Hint(m_result);
    }
}

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
    private readonly EditorTextView m_text = new(interactions);
    private string m_title = "我的第一个故事";
    private string m_theme = "Clear";
    private NovelTemplateKind m_template = NovelTemplateKind.Branching;
    private string m_sample = string.Empty;
    private string m_language = "zh-CN";
    private (float width, float height) m_resolution = (1920, 1080);
    private string m_result = string.Empty;

    internal void Draw()
    {
        EditorUiSession.current.widgets.Hint(m_text.Localize("创建可运行游戏：生成可编辑页面、分支章节、原生材质与启动场景。选择 Play 后在 Game 视图中游玩。"));
        ImGui.InputText(m_text.Label("游戏标题"), ref m_title, 256);
        var widgets = EditorUiSession.current.widgets;
        string TemplateName(NovelTemplateKind kind) => kind switch { NovelTemplateKind.Blank => "空白视觉小说", NovelTemplateKind.Branching => "短篇分支故事", _ => "演出与界面功能展示" };
        if (widgets.BeginBoundedCombo(m_text.Label("项目模板 / 教学样例"), m_text.Localize(m_sample.Length == 0 ? TemplateName(m_template) : TutorialSampleCatalog.entries.Single(sample => sample.id == m_sample).title)))
        {
            try
            {
                foreach (var kind in Enum.GetValues<NovelTemplateKind>()) if (ImGui.Selectable(m_text.Label(TemplateName(kind)), m_sample.Length == 0 && kind == m_template)) { m_template = kind; m_sample = string.Empty; }
                foreach (var sample in TutorialSampleCatalog.entries) if (ImGui.Selectable(m_text.Caption("教学 · " + sample.title, m_text.Localize("教学 · ") + m_text.Localize(sample.title)), m_sample == sample.id)) m_sample = sample.id;
            }
            finally { widgets.EndBoundedCombo(); }
        }
        if (m_sample.Length > 0) widgets.Hint(m_text.Localize(TutorialSampleCatalog.entries.Single(sample => sample.id == m_sample).description));
        if (widgets.BeginBoundedCombo(m_text.Label("游戏语言"), m_language == "zh-CN" ? "简体中文" : "English"))
        {
            try { if (ImGui.Selectable(m_text.Label("简体中文"), m_language == "zh-CN")) m_language = "zh-CN"; if (ImGui.Selectable("English", m_language == "en")) m_language = "en"; }
            finally { widgets.EndBoundedCombo(); }
        }
        if (widgets.BeginBoundedCombo(m_text.Label("设计分辨率"), m_resolution.width + " × " + m_resolution.height))
        {
            try { foreach (var size in new (float width, float height)[] { (1280, 720), (1920, 1080), (2560, 1440), (1920, 1200) })
                if (ImGui.Selectable(size.width + " × " + size.height, m_resolution == size)) m_resolution = size; }
            finally { widgets.EndBoundedCombo(); }
        }
        if (widgets.BeginBoundedCombo(m_text.Label("主题"), m_theme))
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
            if (ImGui.Button(m_text.Label("创建视觉小说 / Create visual novel")))
            {
                var service = new NovelGameTemplateService(documents.codec);
                NovelProjectAsset game = m_sample.Length == 0 ? service.Create(m_title, m_theme, m_template, m_language, m_resolution.width, m_resolution.height)
                    : service.CreateSample(m_sample, m_title, m_theme, m_language, m_resolution.width, m_resolution.height);
                using EditorHistoryTransaction transaction = interactions.history.BeginTransaction("创建 VN 启动场景");
                GameScene scene = sceneEdits.CreateScene("创建 VN 启动场景");
                sceneEdits.RenameScene(scene, "NovelStart");
                GameObject root = sceneEdits.CreateGameObject(scene);
                sceneEdits.RenameGameObject(root, "Visual Novel Player");
                var player = (NovelPlayerComponent)sceneEdits.AddComponent(root, typeof(NovelPlayerComponent));
                properties.ChangeProperty(player, nameof(NovelPlayerComponent.project), () => player.project = game, "设置 VN 游戏定义", null, null);
                string directory = game.assetPath.localPath[..game.assetPath.localPath.LastIndexOf('/')];
                string scenePath = scenes.Save(scene, directory);
                transaction.Commit();
                m_result = m_text.Localize("已创建：") + game.assetPath + m_text.Localize("；启动场景：") + scenePath;
            }
        }
        finally { ImGui.EndDisabled(); }
        if (m_result.Length > 0) widgets.Hint(m_result);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Assets;
using InnoEngine.Rendering;
using InnoEngine.Text;
using InnoEditor.Assets;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Authoring.BuiltInResources;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.Variables;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Authoring.Diagnostics;

namespace Inno.VisualNovel.Authoring.Templates;

/// <summary>Instantiates editable game sources through the native asset mutation service.</summary>
public sealed class NovelGameTemplateService(StoryNodeDataCodec codec)
{
    /// <summary>Creates a game definition, story, material and implemented shell pages in a unique project directory.</summary>
    /// <param name="title">The author-facing game title.</param>
    /// <param name="theme">The initially selected complete theme family.</param>
    /// <param name="kind">The editable blank, branching or multi-chapter showcase starting point.</param>
    /// <param name="language">The initial game language, Chinese or English.</param>
    /// <param name="width">The positive game design width.</param>
    /// <param name="height">The positive game design height.</param>
    /// <returns>The canonical imported game definition.</returns>
    public NovelProjectAsset Create(
        string title,
        string theme,
        NovelTemplateKind kind = NovelTemplateKind.Branching,
        string language = "zh-CN",
        float width = 1920,
        float height = 1080
    ) => CreateGame(title, theme, kind, language, width, height, string.Empty);
    /// <summary>Instantiates a tutorial into writable Game assets with fresh native identities and no sample-subtree dependency.</summary>
    /// <param name="sampleId">One declared tutorial identity.</param>
    /// <param name="title">The game title.</param>
    /// <param name="theme">The complete theme family.</param>
    /// <param name="language">The selected game language.</param>
    /// <param name="width">The design canvas width.</param>
    /// <param name="height">The design canvas height.</param>
    /// <returns>A canonical independent game; the scripting lesson additionally requires its documented command registration.</returns>
    public NovelProjectAsset CreateSample(
        string sampleId,
        string title,
        string theme,
        string language = "zh-CN",
        float width = 1920,
        float height = 1080
    ) {
        if (!TutorialSampleCatalog.entries.Any(sample => sample.id == sampleId)) throw new ArgumentException("Unknown tutorial sample: " + sampleId, nameof(sampleId));
        return CreateGame(title, theme, NovelTemplateKind.Showcase, language, width, height, sampleId);
    }
    private NovelProjectAsset CreateGame(
        string title,
        string theme,
        NovelTemplateKind kind,
        string language,
        float width,
        float height,
        string sampleId
    ) {
        if (!Enum.IsDefined(kind) || language is not ("zh-CN" or "en") || !float.IsFinite(width) || !float.IsFinite(height) || width < 320 || height < 240 || width > 16384 || height > 16384)
            throw new ArgumentException("Choose a declared template, language and a bounded design resolution.");
        if (string.IsNullOrWhiteSpace(title) || title.Length > 256 || theme is not ("Clear" or "Paper" or "Cinema")) throw new ArgumentException("The game needs a bounded title and one complete theme family.");
        string directory = "Game/Novel-" + Guid.NewGuid().ToString("N")[..8];
        var canvas = new TemplateCanvasAdapter(width, height, codec);
        FontAsset font = BuiltInNovelAssets.Load<FontAsset>("BuiltIn/Fonts/NotoSansCJKsc-Regular.otf");
        ShaderAsset shader = BuiltInNovelAssets.Load<ShaderAsset>("BuiltIn/Shaders/Graphs/NovelSurface.ishader");
        var material = Save(directory + "/Presentation.imaterial", new MaterialAsset { shader = shader });
        var effectsMaterial = Save(directory + "/ImageComposition.imaterial", new MaterialAsset { shader = BuiltInNovelAssets.Load<ShaderAsset>("BuiltIn/Shaders/Graphs/NovelPostProcess.ishader") });
        var stages = kind == NovelTemplateKind.Blank ? [] : BuiltInVisualLibrary.Stages().Select(stage => Save(directory + "/Stages/" + stage.resourceId["builtin.stage.".Length..] + ".ivnstage", canvas.Stage(stage))).ToArray();
        var characters = kind == NovelTemplateKind.Blank ? [] : BuiltInVisualLibrary.Characters().Select(character => Save(directory + "/Characters/" + character.resourceId["builtin.character.".Length..] + ".ivncharacter", CharacterAssetCopy.Copy(character))).ToArray();
        var chapters = new List<StoryAsset>(); var chapterSources = new List<StoryDraftState>(); StoryAsset story;
        if (sampleId.Length > 0)
        { story = SaveChapter(directory + "/Opening.ivnstory", canvas.Story(TutorialSampleCatalog.Create(sampleId, codec))); chapters.Add(story); }
        else if (kind == NovelTemplateKind.Showcase)
        {
            var logic = SaveChapter(directory + "/Chapters/Branching.ivnstory", canvas.Story(TutorialStoryTemplates.Branching(codec)));
            var performance = SaveChapter(directory + "/Chapters/Performance.ivnstory", canvas.Story(PerformanceStoryTemplate.Create(codec)));
            var ui = SaveChapter(directory + "/Chapters/Interface.ivnstory", canvas.Story(TutorialStoryTemplates.Interface(codec)));
            story = SaveChapter(directory + "/Opening.ivnstory", ShowcaseStoryTemplate.Create(codec, [("logic", logic.chapterId, "分支、循环与调用"),
                ("performance", performance.chapterId, "角色、演出与效果"), ("interface", ui.chapterId, "界面与阅读布局")])); chapters.AddRange([story, logic, performance, ui]);
        }
        else { story = SaveChapter(directory + "/Opening.ivnstory", canvas.Story(kind == NovelTemplateKind.Blank ? TutorialStoryTemplates.Blank(codec) : StarterPresentationTemplate.Create(codec))); chapters.Add(story); }
        var shared = new Dictionary<string, VariableDefinition>(StringComparer.Ordinal);
        foreach (var variable in chapterSources.SelectMany(static chapter => chapter.variables).Where(static item => item.scope != VariableScope.Local))
        {
            if (shared.TryGetValue(variable.id, out var existing) && !StoryVariableDeclarations.Equivalent(existing, variable))
                throw new AuthoringValidationException($"项目模板包含冲突的共享变量：{variable.id}");
            shared.TryAdd(variable.id, VariableValueRules.Copy(variable));
        }
        var variableTable = Save(directory + "/Variables.ivnvariables", new VariableTableAsset { variables = shared.Values.ToArray() });
        for (int index = 0; index < chapters.Count; index++)
        {
            var source = chapterSources.Single(item => item.chapterId == chapters[index].chapterId);
            StoryVariableBindingService.Attach(source, variableTable, true);
            chapters[index] = Save(chapters[index].assetPath.localPath, source.ToAsset());
        }
        story = chapters[0];
        var themes = new List<ThemeAsset>();
        foreach (string name in ShellThemeTemplates.names)
        {
            ThemeAsset definition = ShellThemeTemplates.Create(name, font);
            foreach (var binding in definition.screens) binding.screen = Save(directory + "/Themes/" + name + "/Screens/" + binding.role + ".ivnui", binding.screen!);
            themes.Add(Save(directory + "/Themes/" + name + "/" + name + ".ivntheme", definition));
        }
        ThemeAsset selectedTheme = themes.Find(item => item.title == theme) ?? throw new ArgumentException("Choose Clear, Paper or Cinema.", nameof(theme));
        AudioCueAsset music = BuiltInNovelAssets.Load<AudioCueAsset>("BuiltIn/Audio/Music/Dawn.ivnaudio");
        var game = new NovelProjectAsset
        {
            gameId = "novel-" + Guid.NewGuid().ToString("N"), title = title, chapters = chapters.ToArray(), startChapter = story.chapterId, defaultLanguage = language, width = width, height = height,
            commandDefinitions = sampleId == "ScriptExtension" ? [ExtensionStoryTemplate.Definition()] : [],
            screens = selectedTheme.screens, themes = themes.ToArray(), defaultTheme = selectedTheme.resourceId, font = font, material = material, effectsMaterial = effectsMaterial,
            stages = stages, characters = characters, initialStage = kind == NovelTemplateKind.Blank ? "" : "builtin.stage.classroom",
            performances = kind == NovelTemplateKind.Showcase ? BuiltInResourceCatalog.entries.Where(static entry => entry.kind == BuiltInResourceKind.Motion).Select(entry =>
                Save(directory + "/Motions/" + System.IO.Path.GetFileName(entry.path), canvas.Motion(BuiltInNovelAssets.Load<PerformanceClipAsset>(entry.path)))).ToArray() : [],
            particles = kind == NovelTemplateKind.Showcase ? BuiltInResourceCatalog.entries.Where(static entry => entry.kind == BuiltInResourceKind.Particle).Select(entry =>
                Save(directory + "/Particles/" + System.IO.Path.GetFileName(entry.path), canvas.Particles(BuiltInNovelAssets.Load<VisualEffectAsset>(entry.path)))).ToArray() : [],
            screenEffects = BuiltInImageEffectLibrary.Screens(), transitions = BuiltInImageEffectLibrary.Transitions(),
            collections = Save(directory + "/Collections.ivncollection", new Model.Collections.CollectionAsset { entries = [
                new() { id = "music.dawn", kind = Model.Collections.CollectionKind.Music, title = "晨光", initiallyUnlocked = true, musicCueId = music.resourceId },
                new() { id = "replay.opening", kind = Model.Collections.CollectionKind.Replay, title = "序章回想", description = "从明确的序章入口重新体验故事，不影响正在游玩的进度。", initiallyUnlocked = true, chapterId = story.chapterId },
                new() { id = "glossary.vn", kind = Model.Collections.CollectionKind.Glossary, title = "视觉小说", description = "以文字、画面与声音共同讲述故事。你可以在 VN 收藏中编辑这个词条。", initiallyUnlocked = true } ] }),
            audio = [music], titleAudio = music.clip
        };
        game.localization = Save(directory + "/Translations.ivnlocale", StarterLocalizationTemplate.Create(game));
        return Save(directory + "/Game.ivnproject", game);

        StoryAsset SaveChapter(
            string path,
            StoryDraftState source
        ) { chapterSources.Add(source); return Save(path, source.ToAsset()); }
    }

    private static T Save<T>(string path, T source) where T : AssetObject
    {
        AssetPath destination = AssetPath.Project(path);
        if (!EditorAssets.Save(destination, source)) throw new AuthoringValidationException($"原生资源导入失败：{path}。已创建资源保留，请查看引擎诊断。");
        return Assets.Load<T>(destination);
    }

}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Inno.Assets;
using Inno.Assets.Pipeline;
using Inno.Core.Diagnostics;
using Inno.Core.Identity;
using Inno.Core.Logging;
using Inno.Core.Serialization;
using Inno.Extensibility.Modules;
using Inno.Extensibility.Types;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Narrative.Extensions;
using Inno.VisualNovel.Player.Ports;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Shell;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class TutorialTemplateContracts
{
    internal static void NativeFontReference(
        string project,
        ModuleHost modules,
        TypeCatalog types,
        SerializationRegistry native,
        IdentityAllocator identities
    ) {
        string root = Path.Combine(project, "artifacts", "AcceptanceProjects", "FontReference-" + Guid.NewGuid().ToString("N"));
        string source = Path.Combine(root, "Assets"); Directory.CreateDirectory(source);
        string plugin = Path.Combine(root, "ReadOnlyPlugin"); Directory.CreateDirectory(Path.Combine(plugin, "BuiltIn", "Fonts"));
        foreach (string suffix in new[] { "", ".imeta" }) File.Copy(Path.Combine(project, "Assets", "BuiltIn", "Fonts", "NotoSansCJKsc-Regular.otf" + suffix), Path.Combine(plugin, "BuiltIn", "Fonts", "NotoSansCJKsc-Regular.otf" + suffix));
        File.Copy(Path.Combine(project, "Assets", "BuiltIn.imeta"), Path.Combine(plugin, "BuiltIn.imeta"));
        File.Copy(Path.Combine(project, "Assets", "BuiltIn", "Fonts.imeta"), Path.Combine(plugin, "BuiltIn", "Fonts.imeta"));
        using var logs = new LogRouter(); using var assets = new AssetPipeline(modules, types, native, identities, new DiagnosticHub(), logs,
            AssetPipelineOptions.Create(source, Path.Combine(root, "Library")) with { enableFileSystemWatcher = false, deferUnavailableExtensions = true,
                sourceMounts = [new(AssetSourceId.project, source, false, [new("inno.visualnovel")]), new(new("inno.visualnovel"), plugin, true)] });
        using var scope = AssetExecutionContext.EnterScope(assets);
        Console.WriteLine("Native font importer catalog: " + string.Join(", ", types.GetSubTypesOf<AssetImporter>().Select(type => types.Resolve(type).FullName).Where(static name => name?.Contains("Font", StringComparison.Ordinal) ?? false)));
        var font = Assets.Assets.Load<Inno.Text.FontAsset>(new AssetPath(new("inno.visualnovel"), "BuiltIn/Fonts/NotoSansCJKsc-Regular.otf"));
        Console.WriteLine("Native font result: id=" + font.identity.persistentId + ", path=" + font.assetPath + ", metadata=" + (font.metadata is not null) + ", payload=" + font.runtimePayload.Length + ", missing=" + font.isMissing);
        Check(font.metadata is not null && font.assetPath.localPath.Length > 0, "Native font importer did not produce a source-owned font: " + font.assetPath);
        var page = ShellScreenTemplates.Create("title", font, "Clear");
        var before = assets.CaptureProperties(page).dependencies.Single();
        Check(before.lastKnownPath.Length > 0, "The source page lost its canonical font path before saving.");
        Check(EditorAssets.Save(AssetPath.Project("Page.ivnui"), page), "Native page import failed.");
        var imported = Assets.Assets.Load<Model.Interface.ScreenAsset>(AssetPath.Project("Page.ivnui"));
        var after = assets.CaptureProperties(imported).dependencies.Single();
        Check(imported.font?.metadata is not null && after.lastKnownPath.Length > 0 && after.persistentId == before.persistentId,
            "Native page decoding replaced its imported font with an unowned reference: before=" + before.lastKnownPath + ", after=" + after.lastKnownPath + ", current=" + imported.font?.assetPath);
    }
    internal static void GraphsAndExtension(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        foreach (var sample in TutorialSampleCatalog.entries)
        {
            var first = TutorialSampleCatalog.Create(sample.id, codec); var second = TutorialSampleCatalog.Create(sample.id, codec);
            Check(first.chapterId != second.chapterId, "A tutorial factory reused chapter identity: " + sample.id);
            var result = compiler.Compile(first.ToAsset()); Check(result.success, sample.id + ": " + string.Join("; ", result.diagnostics.Select(static issue => issue.message)));
        }
        var chapter = Compile(TutorialStoryTemplates.Branching(codec).ToAsset(), compiler);
        using var story = new StoryMachine(chapter.program!); story.Start();
        int choices = 0;
        for (int count = 0; count < 80 && story.status != StoryStatus.Completed; count++)
        {
            if (story.status == StoryStatus.Input) story.SubmitInput();
            else if (story.status == StoryStatus.Dialogue) story.Advance();
            else if (story.status == StoryStatus.Choice) { story.Choose(choices++ == 0 ? "sea" : "forest"); }
            else story.Tick(.1);
        }
        Check(story.status == StoryStatus.Completed && choices == 2 && story.GetVariable("visits").integer == 2 && story.GetVariable("route").text == "森林",
            "The branching tutorial did not execute its real bounded loop, shared merge and call return.");
        chapter = Compile(ExtensionStoryTemplate.Create(codec).ToAsset(), compiler); var commands = new NovelCommandRegistry();
        commands.Register(static () => new Samples.ScriptExtension.DelayScoreCommand());
        using var extension = new StoryMachine(chapter.program!, commands: commands, preview: true); extension.Start(); extension.Advance(); extension.Advance(); extension.Tick(.25);
        Check(extension.status == StoryStatus.Command, "The shipped script lesson does not match the executable C# schema.");
        var saved = native.Deserialize<StoryExecutionState>(native.Serialize(extension.Capture())); extension.Tick(1);
        Check(extension.GetVariable("score").integer == 10, "The sample command result did not reach the declared typed variable.");
        extension.Restore(saved); extension.Tick(.75);
        Check(extension.status == StoryStatus.Dialogue && extension.dialogueText.Contains("10", StringComparison.Ordinal), "The actual distributed command failed neutral continuation restoration.");
    }
    internal static string NativeInstantiation(
        string project,
        ModuleHost modules,
        TypeCatalog types,
        SerializationRegistry native,
        IdentityAllocator identities,
        StoryNodeDataCodec codec
    ) {
        identities = new IdentityAllocator(); using var identityScope = identities.EnterScope();
        string root = Path.Combine(project, "artifacts", "AcceptanceProjects", "Templates-" + Guid.NewGuid().ToString("N"));
        string source = Path.Combine(root, "Assets"); Directory.CreateDirectory(source);
        CopyTree(Path.Combine(project, "Assets", "BuiltIn"), Path.Combine(source, "BuiltIn"));
        File.Copy(Path.Combine(project, "Assets", "BuiltIn.imeta"), Path.Combine(source, "BuiltIn.imeta"));
        using var logs = new LogRouter(); using var assets = new AssetPipeline(modules, types, native, identities, new DiagnosticHub(), logs,
            AssetPipelineOptions.Create(source, Path.Combine(root, "Library")) with { enableFileSystemWatcher = false, deferUnavailableExtensions = true,
                sourcePolicy = new(null, ["Code", "Documentation", "~Samples"], null, null) });
        using var scope = AssetExecutionContext.EnterScope(assets);
        int preparedLeaves = 0;
        foreach (string leaf in Directory.EnumerateFiles(Path.Combine(source, "BuiltIn"), "*", SearchOption.AllDirectories)
            .Where(static path => Path.GetExtension(path).ToLowerInvariant() is ".png" or ".wav" or ".otf").OrderBy(static path => path, StringComparer.Ordinal))
        {
            var path = AssetPath.Project(Path.GetRelativePath(source, leaf).Replace('\\', '/'));
            var loaded = Assets.Assets.Load<AssetObject>(path);
            if (loaded.assetPath.localPath.Length > 0 && !loaded.isMissing) continue;
            Check(assets.Import(path), "The selected headless host could not prepare its leaf dependency: " + path); preparedLeaves++;
        }
        Console.WriteLine("HOST PRECONDITION: explicitly prepared " + preparedLeaves + " incomplete leaf dependencies through the public host importer. Automatic cold-host reference hydration remains unverified.");
        foreach (string shader in Directory.EnumerateFiles(Path.Combine(source, "BuiltIn", "Shaders"), "*", SearchOption.AllDirectories)
            .Where(static path => Path.GetExtension(path) is ".ishadersource" or ".ishader").OrderBy(static path => path, StringComparer.Ordinal))
        {
            var path = AssetPath.Project(Path.GetRelativePath(source, shader).Replace('\\', '/'));
            Check(assets.Import(path), "The headless host could not prepare its shader dependency: " + path);
        }
        Console.WriteLine("HOST PRECONDITION: prepared source and graph shader artifacts through the public importer before template creation.");
        foreach (var entry in Inno.VisualNovel.Authoring.BuiltInResources.BuiltInResourceCatalog.entries)
        {
            var loaded = Inno.VisualNovel.Authoring.BuiltInResources.BuiltInResourceLoader.Load(entry);
            Check(!loaded.isMissing && loaded.assetPath.localPath == entry.path, "The shared thumbnail/detail loader resolved a wrong or missing native source: " + entry.path);
            if (loaded is Inno.VisualNovel.Model.Audio.AudioCueAsset { clip: { } clip })
                Check(Inno.VisualNovel.Authoring.VoiceManagement.NativeWaveformSource.Read(clip).BuildPeaks(64).Count > 0,
                    "A shipped audio thumbnail could not read its real native waveform without opening an audio device.");
        }
        var service = new NovelGameTemplateService(codec);
        var blank = service.Create("空白验收", "Paper", NovelTemplateKind.Blank, "en", 1280, 720);
        Check(blank.font is { metadata: not null } && blank.font.assetPath.localPath.Length > 0, "The headless authoring host accepted a font placeholder without a native importer.");
        Check(blank.chapters.Single().program!.instructions.Length == 3 && blank.stages.Length == 0 && blank.characters.Length == 0 && blank.defaultLanguage == "en",
            "The blank template retained hidden story presentation or ignored wizard settings.");
        var branching = service.Create("分支起点验收", "Clear", NovelTemplateKind.Branching, "zh-CN", 1280, 720);
        Check(branching.chapters.Single().program!.instructions.Length == 9 && branching.characters.Length == 2 && branching.stages.Length == 6,
            "The branching project start lost its actual beginner presentation or imported libraries.");
        using (var session = new NovelSession(branching, new SilentOutput()))
        {
            session.StartNew(); session.Advance(); session.Advance(); session.Choose("sea");
            var scenery = branching.stages.Single(static stage => stage.resourceId == "builtin.stage.seafront");
            Guid texture = scenery.textures[scenery.layers[0].texture].identity.persistentId;
            Check(session.story!.status == StoryStatus.Dialogue && session.stage.Capture().layers.Any(layer => layer.owner.StartsWith("stage/", StringComparison.Ordinal) && layer.textureId == texture),
                "The native branching project did not change to its actual imported scenery: " + session.story.status + " / " + session.story.error);
            session.Advance(); session.Advance(); Check(session.story.status == StoryStatus.Completed, "The native branching project did not complete its authored path.");
        }
        var showcase = service.Create("功能展示验收", "Cinema", NovelTemplateKind.Showcase, "zh-CN", 1280, 720);
        PreviewDraftContracts.Run(root, types, native, identities, assets, logs, showcase);
        Check(showcase.chapters.Length == 4 && showcase.themes.Length == 3 && showcase.performances.Length == 20 && showcase.particles.Length == 8,
            "The showcase did not instantiate its actual lessons and native editable resource declarations.");
        var dependencies = assets.GetDependencies(showcase, true);
        Check(dependencies.All(static identity => identity.persistentId != Guid.Empty) && showcase.chapters.All(static item => item.assetPath.localPath.StartsWith("Game/", StringComparison.Ordinal)),
            "A generated game retained sample subtree sources or detached dependency identities.");
        Check(showcase.stages.All(static stage => stage.width == 1280 && stage.height == 720 && Math.Abs(stage.layers[0].transform.scaleX - 2d / 3) < 1e-9), "Template scenery was cropped instead of fitted into the selected design canvas.");
        foreach (string route in new[] { "logic", "performance", "interface" })
        {
            using var session = new NovelSession(showcase, new SilentOutput()); using var view = new NovelGameView(showcase, session, static (_, _) => (160, 40)); session.StartNew();
            int choices = 0;
            for (int count = 0; count < 400 && session.story!.status is not (StoryStatus.Completed or StoryStatus.Faulted); count++)
            {
                view.BuildFrame();
                if (session.page != "reading") { view.HandleInput(new() { cancel = true }); continue; }
                if (session.story.status == StoryStatus.Dialogue) session.Advance();
                else if (session.story.status == StoryStatus.Input) session.story.SubmitInput();
                else if (session.story.status == StoryStatus.Choice) session.Choose(choices++ == 0 ? route : choices % 2 == 0 ? "sea" : "forest");
                else session.Tick(.1);
            }
            Check(session.story!.status == StoryStatus.Completed, "Native showcase route did not finish through the real session and editable shell: " + route + " / " + session.story.status + " / " + session.story.error);
        }
        var sample = service.CreateSample("FirstStory", "独立样例验收", "Clear");
        Check(sample.gameId != showcase.gameId && sample.chapters[0].chapterId != showcase.chapters[0].chapterId
            && sample.themes[0].identity.persistentId != showcase.themes[0].identity.persistentId, "Independent template instances shared writable root or nested page identities.");
        if (Environment.GetEnvironmentVariable("INNO_VN_PREPARE_GAME_BUILD") == "1") GameBuildFixture.Write(project, root, branching, identities, types, native, assets);
        Console.WriteLine("Native template acceptance project: " + root);
        return root;
    }
    private static void CopyTree(
        string source,
        string destination
    ) {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.EnumerateFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (string directory in Directory.EnumerateDirectories(source)) CopyTree(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }
    private static StoryAsset Compile(
        StoryAsset chapter,
        StoryCompiler compiler
    ) { var result = compiler.Compile(chapter); Check(result.success, string.Join("; ", result.diagnostics.Select(static issue => issue.message))); chapter.program = result.program; return chapter; }
    private sealed class SilentOutput : INovelAudioOutput
    {
        private long m_next; private readonly HashSet<long> m_tokens = [];
        public long Play(string cueId, double gain, bool loop, double position) { long token = ++m_next; m_tokens.Add(token); return token; }
        public void Stop(long token) => m_tokens.Remove(token);
        public void SetGain(long token, double gain) { }
        public void SetPaused(long token, bool paused) { }
        public bool IsActive(long token) => m_tokens.Contains(token);
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}

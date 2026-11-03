using System;
using System.IO;
using System.Linq;
using Inno.Adapter.Modules.DotNet;
using Inno.Adapter.Serialization.DotNet;
using Inno.Assets;
using Inno.Assets.Pipeline;
using Inno.Core.Identity;
using Inno.Core.Input;
using Inno.Core.Logging;
using Inno.Core.Mathematics;
using Inno.Core.Serialization;
using Inno.Editor.Core;
using Inno.Editor.Inspection;
using Inno.Editor.Interactions;
using Inno.Editor.Scene;
using Inno.Extensibility.Types;
using Inno.Plugins.Authoring;
using Inno.Runtime;
using Inno.Scene;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.SceneEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Integration.Components;
using Inno.VisualNovel.Integration.Components.Interface;
using Inno.VisualNovel.Integration.Hosting;
using Inno.VisualNovel.Integration.Rendering;
using Inno.VisualNovel.Model.Project;

namespace Inno.VisualNovel.Tests.Contracts;

/// <summary>Rebuilds consumer fixtures through the installed plugin's compiled native authoring path, never through a compatibility reader.</summary>
internal static class ConsumerSceneFixture
{
    internal static void Rebuild(
        string repository,
        string project,
        bool verifyOnly = false
    ) {
        project = Path.GetFullPath(project);
        string allowed = Path.Combine(Path.GetFullPath(repository), "artifacts", "AcceptanceProjects") + Path.DirectorySeparatorChar;
        string workspace = Path.GetDirectoryName(Path.GetFullPath(repository))!;
        string[] consumers = [Path.Combine(workspace, "InnoEngine.TestProject"), Path.Combine(workspace, "InnoEngine.Samples", "NeonZero")];
        if (!project.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)
            && !(verifyOnly && consumers.Contains(project, StringComparer.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Rebuild inside acceptance candidates; read-only content verification also permits the two authorized consumers.");
        var sceneAssets = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory, "Inno.Scene.Assets.dll"));
        using var host = new EngineHostBuilder().UseMetadataSources(new DotNetAssemblyCatalogSource(typeof(ConsumerSceneFixture).Assembly,
            typeof(SceneEdits).Assembly, sceneAssets, typeof(Inno.Text.Assets.FontImporter).Assembly,
            typeof(Inno.Audio.Assets.AudioClipImporter).Assembly, typeof(Inno.Adapter.UI.RmlUi.Authoring.RmlUiDocumentFrontend).Assembly,
            typeof(Inno.Rendering.Assets.Authoring.ShaderGraphSourceStore).Assembly,
            typeof(Inno.Build.Toolchains.Bgfx.Tools.BgfxShaderSourceFrontend).Assembly),
            new ReflectionTypeCatalogSource(), new ReflectionSerializationMetadataSource()).Build();
        _ = host.modules.Register("vn.consumer.runtime", [typeof(NovelSceneSystem).Assembly, typeof(NovelProjectAsset).Assembly,
            typeof(Player.Sessions.NovelSession).Assembly, typeof(Narrative.Execution.StoryMachine).Assembly,
            typeof(Stage.Composition.StageController).Assembly, typeof(UI.Documents.UiDocumentInstance).Assembly,
            typeof(Rendering.Frames.NovelFrameComposer).Assembly]);
        _ = host.modules.Register("vn.consumer.authoring", [typeof(NovelSceneEditService).Assembly, typeof(StoryCompiler).Assembly]);
        using var log = new FileLogSink(Path.Combine(project, "Logs", "NativeRebuild")); host.logs.RegisterSink(log);
        using var session = host.CreateSession(new RuntimeSessionOptions { kind = RuntimeSessionKind.Edit,
            applicationId = "inno.vn.consumer-rebuild", jobExecutionMode = RuntimeJobExecutionMode.SingleThread,
            createLogSink = _ => new FileLogSink(Path.Combine(project, "Logs", "NativeRebuildSession")) });
        using var execution = session.EnterExecutionScope();
        string library = Path.Combine(project, "Library");
        var plugins = new PluginSourceService(host.serialization, Path.Combine(project, "Plugins"), library).Scan();
        if (plugins.diagnostics.Count != 0) throw new InvalidOperationException("Consumer plugin scan failed.");
        var options = AssetPipelineOptions.Create(Path.Combine(project, "Assets"), library);
        using var assets = new AssetPipeline(host.modules, host.types, host.serialization, new IdentityAllocator(), host.diagnostics, host.logs,
            options with { enableFileSystemWatcher = false, sourceMounts = [.. options.sourceMounts!, .. PluginSourceService.GetActivatableMounts(plugins)] });
        using var assetScope = AssetExecutionContext.EnterScope(assets);
        using var editor = new EditorInteractionRuntime(new EditorContext(project, new EditorKeyboardPolicy(KeyModifier.Control, "Ctrl")),
            host.types, host.logs, [host.types, host.serialization, session, assets, new EditorReloadCoordinator()]);
        editor.Start(); _ = editor.panelCount;
        if (!editor.interactions.TryGetModule<SceneEdits>(out var scenes) || scenes is null)
            throw new InvalidOperationException("Native scene edit services did not activate.");
        InspectorActivationContracts.Verify(editor.interactions, scenes);
        if (verifyOnly)
        {
            var main = assets.Load<SceneAsset>(AssetPath.Project("Game/Scenes/Main.iscene")).Instantiate(host.serialization, assets);
            SceneManager.LoadSceneAdditive(main);
            var installedGame = main.GetSystems().OfType<NovelSceneSystem>().Single().project
                ?? throw new InvalidOperationException("The installed Main scene has no VN game definition.");
            var workshop = assets.Load<SceneAsset>(AssetPath.Project("Game/Scenes/NativeUiWorkshop.iscene")).Instantiate(host.serialization, assets);
            SceneManager.LoadSceneAdditive(workshop);
            var installedCanvas = workshop.GetObjects().SelectMany(static owner => owner.GetComponents<NovelUiRootComponent>()).Single();
            ValidateUi(workshop.GetSystems().OfType<NovelSceneSystem>().Single(), installedCanvas, assets, repository);
            var stage = assets.Load<SceneAsset>(AssetPath.Project(ConsumerStageWorkshop.C_SCENE)).Instantiate(host.serialization, assets);
            SceneManager.LoadSceneAdditive(stage);
            ConsumerStageWorkshop.Validate(stage);
            RunRoute(installedGame, false); RunRoute(installedGame, true);
            Console.WriteLine("Verified installed consumer content without writing source assets: " + project);
            return;
        }
        var properties = new Properties(scenes);
        var codec = new StoryNodeDataCodec(new EditorStorySerialization(host.serialization));
        var gameEntry = assets.GetFileSystemEntries(includeDirectories: false).FirstOrDefault(entry => entry.source == AssetSourceId.project
            && assets.TryGetAssetType(entry.assetPath, out var type) && type == typeof(NovelProjectAsset));
        var game = gameEntry is null ? new NovelGameTemplateService(codec).Create("Native VN Workshop", "Clear", NovelTemplateKind.Branching, "en")
            : assets.Load<NovelProjectAsset>(gameEntry.assetPath);
        var scene = new NovelSceneEditService(scenes, properties).CreateGameScene(game);
        scenes.RenameScene(scene, game.title);
        var system = scene.GetSystems().OfType<NovelSceneSystem>().Single();
        Check(system.project == game && scene.GetObjects().Any(), "The authoring workflow created no native scene content.");
        Save(scene, "Game/Scenes/Main.iscene");
        var reopened = assets.Load<SceneAsset>(AssetPath.Project("Game/Scenes/Main.iscene")).Instantiate(host.serialization, assets);
        SceneManager.LoadSceneAdditive(reopened);
        Check(reopened.GetSystems().OfType<NovelSceneSystem>().Single().project?.identity.persistentId == game.identity.persistentId
            && !reopened.GetObjects().SelectMany(static owner => owner.GetComponents<GameComponent>()).OfType<MissingGameComponent>().Any(),
            "The rebuilt native consumer scene lost its game reference or contains missing types.");
        Console.WriteLine("Rebuilt native Main: " + scene.GetObjects().Count + " objects, one VN System; " + game.chapters.Length + " chapters.");
        SceneManager.UnloadScene(reopened);

        var showcase = scenes.CreateScene(); scenes.RenameScene(showcase, "Native UI Workshop");
        var showcaseSystem = (NovelSceneSystem)scenes.AddSystem(showcase, typeof(NovelSceneSystem));
        showcaseSystem.surfaceMaterial = game.material; showcaseSystem.imageEffectsMaterial = game.effectsMaterial;
        var backdrop = new NovelSceneEditService(scenes, properties).CreateImage(showcase, "Backdrop", null, new(1920, 1080)); backdrop.color = 0x101624FF;
        var ui = new NovelUiEditService(scenes, properties); var canvas = ui.CreateCanvas(showcase);
        var page = ui.CreatePage(canvas); page.role = "workshop";
        var heading = ui.CreateText(page.gameObject); heading.text = "NATIVE SCENE / ENGINE UI"; heading.fontSize = 52;
        heading.gameObject.GetComponent<NovelUiLayoutComponent>().size = new(1500, 90); heading.transform.localPosition = new(0, 390, 0);
        var description = ui.CreateText(page.gameObject); description.text = "Edit objects in Hierarchy and Inspector. Transform, layout, text and Prefab ownership are native.";
        description.gameObject.GetComponent<NovelUiLayoutComponent>().size = new(1500, 90); description.transform.localPosition = new(0, 280, 0);
        var grid = ui.CreateContainer(page.gameObject, Model.Interface.UiContainerLayout.Grid); grid.columns = 2; grid.gap = 28;
        grid.gameObject.GetComponent<NovelUiLayoutComponent>().size = new(1500, 460);
        string[] names = ["Scene ownership", "Engine text", "Native Prefabs", "Shared History"];
        string[] descriptions = ["Delete or disable an object: its presentation disappears.", "中文・日本語・English — rich text and grapheme reveal.",
            "Each instance has real children and remapped references.", "Creation, edits, Undo and Redo use engine services."];
        for (int index = 0; index < names.Length; index++)
        {
            var card = ui.CreateContainer(grid.gameObject, Model.Interface.UiContainerLayout.Vertical); card.gameObject.name = names[index]; card.padding = 24;
            var layout = card.gameObject.GetComponent<NovelUiLayoutComponent>(); layout.widthMode = Model.Interface.UiSizeMode.Fill; layout.size = new(600, 190);
            card.gameObject.AddComponent<NovelUiPanelComponent>().color = index % 2 == 0 ? 0x1D2B42FFu : 0x252B40FFu;
            var title = ui.CreateText(card.gameObject); title.text = names[index]; title.fontSize = 34;
            title.gameObject.GetComponent<NovelUiLayoutComponent>().widthMode = Model.Interface.UiSizeMode.Fill;
            var body = ui.CreateText(card.gameObject); body.text = descriptions[index]; body.fontSize = 24;
            var bodyLayout = body.gameObject.GetComponent<NovelUiLayoutComponent>(); bodyLayout.widthMode = Model.Interface.UiSizeMode.Fill; bodyLayout.size = new(500, 86);
        }
        var dialog = ui.CreatePage(canvas); dialog.gameObject.name = "Native Dialog"; dialog.role = "workshop-dialog"; dialog.modal = true;
        dialog.gameObject.AddComponent<NovelUiPanelComponent>().color = 0x080F1EEE;
        var dialogTitle = ui.CreateText(dialog.gameObject); dialogTitle.text = "A REAL PAGE IN HIERARCHY"; dialogTitle.fontSize = 42;
        dialogTitle.transform.localPosition = new(0, 110, 0); dialogTitle.gameObject.GetComponent<NovelUiLayoutComponent>().size = new(1000, 80);
        var dialogHelp = ui.CreateText(dialog.gameObject); dialogHelp.text = "This modal uses engine UI input. Closing it restores the previous page and its focus.";
        dialogHelp.fontSize = 26; dialogHelp.transform.localPosition = new(0, 20, 0); dialogHelp.gameObject.GetComponent<NovelUiLayoutComponent>().size = new(1100, 70);
        var close = ui.CreatePageLink(dialog.gameObject); close.target = dialog; close.operation = NovelUiPageNavigation.Close;
        close.transform.localPosition = new(0, -110, 0); close.gameObject.GetComponent<NovelUiLayoutComponent>().size = new(360, 70);
        close.gameObject.GetComponent<NovelUiTextComponent>().text = "Back to workshop"; dialog.initialFocus = close.gameObject;
        var open = ui.CreatePageLink(page.gameObject); open.target = dialog;
        open.transform.localPosition = new(0, -330, 0); open.gameObject.GetComponent<NovelUiLayoutComponent>().size = new(360, 70);
        open.gameObject.GetComponent<NovelUiTextComponent>().text = "Open native dialog"; page.initialFocus = open.gameObject;
        dialog.gameObject.SetActive(false);
        var footer = ui.CreateText(page.gameObject); footer.text = "Play to try native page navigation • Edit mode never advances story or audio."; footer.fontSize = 24;
        footer.transform.localPosition = new(0, -440, 0); footer.gameObject.GetComponent<NovelUiLayoutComponent>().size = new(1500, 70);
        Save(showcase, "Game/Scenes/NativeUiWorkshop.iscene");
        var prefab = PrefabAsset.Capture(canvas.gameObject, host.serialization, assets);
        Check(EditorAssets.Save(AssetPath.Project("Game/Prefabs/NativeUiWorkshop.iprefab"), prefab), "The native UI sample Prefab did not save.");
        Console.WriteLine("Rebuilt UI workshop: " + showcase.GetObjects().Count + " real objects; saved native canvas Prefab.");
        ValidateUi(showcaseSystem, canvas, assets, repository);
        ConsumerStageWorkshop.Create(scenes, properties, editor.interactions.history, game, host.serialization, assets);
        RunRoute(game, false); RunRoute(game, true);

        void Save(GameScene value, string path) => Check(EditorAssets.Save(AssetPath.Project(path), SceneAsset.Capture(value, host.serialization, assets)), "Native scene save failed: " + path);
    }

    private static void ValidateUi(
        NovelSceneSystem system,
        NovelUiRootComponent canvas,
        AssetPipeline assets,
        string repository
    ) {
        string native = Path.Combine(AppContext.BaseDirectory, "native");
        string directory = Path.Combine(native, "ui", "windows-x64"); Directory.CreateDirectory(directory);
        File.Copy(Path.Combine(repository, "artifacts", "EngineEditorSnapshot", "native", "ui", "windows-x64", "inno-ui-debug.dll"),
            Path.Combine(directory, "inno-ui-debug.dll"), true);
        var deployment = new Inno.Native.LibraryLoading.NativeDeploymentIndex("windows-x64",
            new System.Collections.Generic.Dictionary<string, string> { ["inno-ui-debug"] = "ui/windows-x64/inno-ui-debug.dll" },
            new System.Collections.Generic.Dictionary<string, string>());
        File.WriteAllBytes(Path.Combine(native, Inno.Native.LibraryLoading.NativeDeploymentIndex.C_FILE_NAME), deployment.Encode());
        using var ui = new Inno.UI.Runtime.UiRuntime(new Inno.Adapter.UI.RmlUi.RmlUiBackend(), assets); ui.Attach();
        using var scope = Inno.UI.UiExecutionContext.EnterScope(ui);
        var first = system.CaptureScene();
        Check(first.uiMeshes.Count > 0 && first.uiMeshes.Any(static mesh => mesh.texture.bitmap is not null), "The installed plugin did not produce actual engine UI glyph geometry.");
        Check(system.CaptureScene().uiMeshes.Count == first.uiMeshes.Count && !system.hasPresentation,
            "Repeated edit capture changed output or started a runtime session.");
        canvas.gameObject.SetActive(false);
        Check(system.CaptureScene().uiMeshes.Count == 0, "Disabling the real canvas left UI geometry in the consumer.");
        canvas.gameObject.SetActive(true);
        Check(system.CaptureScene().uiMeshes.Count > 0, "Re-enabling the real canvas failed to recreate its engine UI resources.");
        var links = system.owningScene.GetObjects().SelectMany(static owner => owner.GetComponents<NovelUiPageLinkComponent>()).ToArray();
        if (links.Length > 0)
        {
            var open = links.Single(link => link.operation == NovelUiPageNavigation.Show);
            var close = links.Single(link => link.operation == NovelUiPageNavigation.Close);
            Activate(open); Check(open.target!.gameObject.activeSelf, "Installed native dialog did not open through the real UI action.");
            Activate(close); Check(!open.target.gameObject.activeSelf && !open.gameObject.isDestroyed, "Installed modal close lost its source page.");
            Console.WriteLine("Installed native UI page links: actual engine hit, modal open/close, explicit component references passed.");
        }
        Console.WriteLine("Installed plugin offscreen UI: " + first.uiMeshes.Count + " engine meshes, real multilingual glyphs, native activation and repeated passive capture passed; GPU and visual layout untested.");
        system.StopGame();
        void Activate(NovelUiPageLinkComponent link)
        {
            var position = link.transform.worldPosition;
            Vector2 pointer = new(system.width / 2 + position.x, system.height / 2 - position.y);
            var hit = system.PickSceneUi(new(system.width, system.height, Matrix.identity), pointer);
            _ = system.HandleSceneInput(new(pointer, Vector2.ZERO, 0, [], [], [MouseButton.Left], [], []));
            var actions = system.HandleSceneInput(new(pointer, Vector2.ZERO, 0, [], [], [], [MouseButton.Left], [])).actions;
            if (actions.Count != 1) throw new InvalidOperationException("Native page action at " + pointer + " hit " + hit?.name
                + "; expected " + link.gameObject.name + "; actions=" + actions.Count);
            var action = actions.Single();
            system.InvokeSceneAction(action);
        }
    }

    private static void RunRoute(NovelProjectAsset project, bool lastChoice)
    {
        using var session = new Player.Sessions.NovelSession(project, new SilentAudio()); session.StartNew();
        for (int step = 0; step < 100000; step++)
        {
            var story = session.interactiveStory ?? session.story!;
            if (story.status == Narrative.Execution.StoryStatus.Faulted) throw new InvalidOperationException(story.error);
            if (story.status == Narrative.Execution.StoryStatus.Completed) { Console.WriteLine("Narrative route " + (lastChoice ? "last" : "first") + ": completed; " + session.history.Count + " history entries; audio device output untested."); return; }
            if (story.status is Narrative.Execution.StoryStatus.Dialogue or Narrative.Execution.StoryStatus.AdvanceWait) session.Advance();
            else if (story.status == Narrative.Execution.StoryStatus.Choice)
            { var choices = story.choices.Where(static choice => choice.enabled).ToArray(); session.Choose((lastChoice ? choices[^1] : choices[0]).id); }
            else if (story.status == Narrative.Execution.StoryStatus.Input) { story.SetInputText("Player"); session.SubmitInput(); }
            if (session.page != "reading") session.OpenPage("reading");
            session.Tick(.5);
        }
        throw new InvalidOperationException("Consumer route did not complete within its headless step budget.");
    }
    private sealed class SilentAudio : Player.Ports.INovelAudioOutput
    {
        private long m_token;
        public long Play(string cueId, double gain, bool loop, double position) => ++m_token;
        public void Stop(long token) { }
        public void SetGain(long token, double gain) { }
        public void SetPaused(long token, bool paused) { }
        public bool IsActive(long token) => false;
    }
    private sealed class Properties(SceneEdits scenes) : IInspectionPropertyEditService
    {
        public bool ChangeProperty(object owner, string propertyName, Action mutation, string historyName, string? mergeKey, string? componentName)
            => scenes.ChangeProperty((EngineObject)owner, propertyName, mutation, historyName, mergeKey);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

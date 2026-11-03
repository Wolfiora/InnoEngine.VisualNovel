using System;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
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
using Inno.Runtime;
using Inno.Scene;
using Inno.VisualNovel.Authoring.SceneEditing;
using Inno.VisualNovel.Integration.Components;
using Inno.VisualNovel.Integration.Rendering;
using Inno.VisualNovel.Integration.Components.Interface;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class NativeSceneHistoryContracts
{
    internal static void CreateUndoPrefabAndReopen(string repository)
    {
        string root = Path.Combine(repository, "artifacts", "AcceptanceProjects", "NativeSceneHistory-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Assets", "Game"));
        Directory.CreateDirectory(Path.Combine(root, "Assets", "BuiltIn", "Fonts"));
        File.Copy(Path.Combine(repository, "Assets", "BuiltIn", "SceneUi.rml"), Path.Combine(root, "Assets", "BuiltIn", "SceneUi.rml"));
        File.Copy(Path.Combine(repository, "Assets", "BuiltIn", "Fonts", "NotoSansCJKsc-Regular.otf"), Path.Combine(root, "Assets", "BuiltIn", "Fonts", "NotoSansCJKsc-Regular.otf"));
        var sceneAssets = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory, "Inno.Scene.Assets.dll"));
        using var host = new EngineHostBuilder().UseMetadataSources(
            new DotNetAssemblyCatalogSource(typeof(NativeSceneHistoryContracts).Assembly, typeof(SceneEdits).Assembly, sceneAssets,
                typeof(Inno.Text.Assets.FontImporter).Assembly, typeof(Inno.Adapter.UI.RmlUi.Authoring.RmlUiDocumentFrontend).Assembly),
            new ReflectionTypeCatalogSource(), new ReflectionSerializationMetadataSource()).Build();
        _ = host.modules.Register("vn.scene-history-contracts", [typeof(NovelImageComponent).Assembly,
            typeof(Model.Story.StoryAsset).Assembly,
            typeof(Player.Sessions.NovelSession).Assembly, typeof(UI.Documents.UiDocumentInstance).Assembly,
            typeof(Stage.Composition.StageController).Assembly, typeof(Narrative.Execution.StoryMachine).Assembly]);
        _ = host.modules.Register("vn.scene-history-editor-contracts", [typeof(NovelSceneEditService).Assembly, typeof(Compiler.StoryCompiler).Assembly]);
        using var sink = new FileLogSink(Path.Combine(root, "HostLogs"));
        host.logs.RegisterSink(sink);
        using var session = host.CreateSession(new RuntimeSessionOptions { kind = RuntimeSessionKind.Edit,
            applicationId = "inno.vn.scene-history", jobExecutionMode = RuntimeJobExecutionMode.SingleThread,
            createLogSink = _ => new FileLogSink(Path.Combine(root, "Logs")) });
        using var execution = session.EnterExecutionScope();
        using var assets = new AssetPipeline(host.modules, host.types, host.serialization, new IdentityAllocator(), host.diagnostics,
            host.logs, AssetPipelineOptions.Create(Path.Combine(root, "Assets"), Path.Combine(root, "Library")) with { enableFileSystemWatcher = false });
        using var assetScope = AssetExecutionContext.EnterScope(assets);
        using var runtime = new EditorInteractionRuntime(new EditorContext(root, new EditorKeyboardPolicy(KeyModifier.Control, "Ctrl")),
            host.types, host.logs, [host.types, host.serialization, session, assets, new EditorReloadCoordinator()]);
        runtime.Start(); _ = runtime.panelCount;
        if (!runtime.interactions.TryGetModule<SceneEdits>(out var scenes) || scenes is null
            || !runtime.interactions.TryGetModule<NativeSceneHistoryProbe>(out var probe) || probe?.workspace is not { } workspace)
            throw new InvalidOperationException("The actual editor host did not activate native scene history services: " + root);
        var properties = new NativeProperties(scenes);
        InspectorActivationContracts.Verify(runtime.interactions, scenes);
        var authoring = new NovelSceneEditService(scenes, properties);
        var scene = scenes.CreateScene();
        var image = authoring.CreateImage(scene, "History image", null, new(100, 80));
        Guid objectId = image.gameObject.identity.persistentId;
        var view = new NovelSceneProjection(800, 600, Matrix.identity);
        Check(NovelSceneCapture.Capture(scene, view).items.Count == 1, "Native image authoring did not create real renderable content.");
        int creationSteps = 0;
        while (runtime.interactions.history.undoName != "Create Scene" && creationSteps < 32)
        {
            var undo = runtime.interactions.history.Undo();
            Check(undo.succeeded, "A native creation step could not undo: " + undo.message); creationSteps++;
        }
        Check(scene.GetObjects().Count == 0 && creationSteps > 1, "Native creation did not undo completely using independent steps.");
        for (int index = 0; index < creationSteps; index++)
        {
            var redo = runtime.interactions.history.Redo();
            Check(redo.succeeded, "A native creation step could not redo: " + redo.message);
        }
        var item = scene.GetObjects().Single(value => value.identity.persistentId == objectId);
        image = item.GetComponent<NovelImageComponent>();
        Check(image.width == 100 && image.height == 80, "Native redo lost the independently authored image properties.");
        scenes.ChangeProperty(item.transform, nameof(item.transform.localPosition), () => item.transform.localPosition = new(90, 30, 0), "Move native image");
        Check(NovelSceneCapture.Capture(scene, view).items.Single().topLeft == new System.Numerics.Vector2(440, 230), "Transform history did not affect actual frame geometry.");
        Check(runtime.interactions.history.Undo().succeeded && item.transform.localPosition == Vector3.ZERO,
            "Native Transform undo did not restore the existing object.");
        string prefabPath = workspace.SavePrefab(item, "Game");
        var prefab = assets.Load<PrefabAsset>(AssetPath.Project(prefabPath));
        var copy = scenes.InstantiatePrefab(prefab, scene);
        Check(copy.identity.persistentId != objectId && NovelSceneCapture.Capture(scene, view).items.Count == 2,
            "Native Prefab save and instantiation did not preserve real independent image objects.");
        var character = (NovelCharacterComponent)scenes.AddComponent(item, typeof(NovelCharacterComponent));
        var part = (NovelCharacterPartComponent)scenes.AddComponent(copy, typeof(NovelCharacterPartComponent));
        copy.transform.SetParent(item.transform);
        scenes.ChangeProperty(part, nameof(part.character), () => part.character = character, "Bind native character owner");
        var target = (NovelTargetComponent)scenes.AddComponent(item, typeof(NovelTargetComponent));
        scenes.ChangeProperty(target, nameof(target.targetId), () => target.targetId = "history/main", "Bind story target");
        var clip = new Model.Performance.PerformanceClipAsset { duration = 1, tracks = [new()
        {
            target = "actor", property = Model.Performance.PerformanceProperty.X,
            keys = [new() { time = 0, value = 0 }, new() { time = 1, value = 120 }]
        }] };
        Check(EditorAssets.Save(AssetPath.Project("Game/Motion.ivnclip"), clip), "Native performance resource did not save.");
        clip = assets.Load<Model.Performance.PerformanceClipAsset>(AssetPath.Project("Game/Motion.ivnclip"));
        var performance = (NovelPerformanceComponent)scenes.AddComponent(item, typeof(NovelPerformanceComponent));
        scenes.ChangeProperty(performance, nameof(performance.clip), () => performance.clip = clip, "Assign performance");
        scenes.ChangeProperty(performance, nameof(performance.bindings), () => performance.bindings = [new() { slot = "actor", target = item }], "Bind performance target");
        new NovelPerformanceEditService(runtime.interactions.history, properties).Seek(performance, .5);
        Check(item.transform.localPosition.x == 60 && performance.elapsed == .5, "Authoring Seek did not apply native component properties.");
        Check(runtime.interactions.history.Undo().succeeded && item.transform.localPosition.x == 0 && performance.elapsed == 0,
            "The native property transaction did not undo the time and every target together.");
        Check(runtime.interactions.history.Redo().succeeded && item.transform.localPosition.x == 60 && performance.elapsed == .5,
            "Existing-object performance properties did not redo atomically.");
        var uiEdits = new NovelUiEditService(scenes, properties);
        var canvas = uiEdits.CreateCanvas(scene);
        var canvasTarget = (NovelTargetComponent)scenes.AddComponent(canvas.gameObject, typeof(NovelTargetComponent));
        scenes.ChangeProperty(canvasTarget, nameof(canvasTarget.targetId), () => canvasTarget.targetId = "history/ui", "Bind UI target");
        var page = uiEdits.CreatePage(canvas);
        var container = uiEdits.CreateContainer(page.gameObject, Model.Interface.UiContainerLayout.Vertical);
        var uiButton = uiEdits.CreateButton(container.gameObject);
        var toggle = uiEdits.CreateToggle(container.gameObject);
        Guid toggleObject = toggle.gameObject.identity.persistentId;
        Check(runtime.interactions.history.Undo().succeeded && !scene.GetObjects().Single(value => value.identity.persistentId == toggleObject).TryGetComponent<NovelUiToggleComponent>(out _),
            "Native UI creation did not enter real component History.");
        Check(runtime.interactions.history.Redo().succeeded && scene.GetObjects().Single(value => value.identity.persistentId == toggleObject).TryGetComponent<NovelUiToggleComponent>(out _),
            "Native UI redo did not restore the actual toggle component.");
        scenes.ChangeProperty(page, nameof(page.initialFocus), () => page.initialFocus = uiButton.gameObject, "Bind initial focus");
        var uiBinding = (NovelUiBindingComponent)scenes.AddComponent(uiButton.gameObject, typeof(NovelUiBindingComponent));
        var bindingEdits = new NovelUiBindingEditService(properties);
        bindingEdits.SetBinding(uiBinding, nameof(uiBinding.text), "dialogue.text");
        Check(runtime.interactions.history.Undo().succeeded && uiBinding.text.Length == 0
            && runtime.interactions.history.Redo().succeeded && uiBinding.text == "dialogue.text", "No-code UI field selection bypassed native property History.");
        var navigation = uiEdits.CreatePageLink(container.gameObject);
        scenes.ChangeProperty(navigation, nameof(navigation.target), () => navigation.target = page, "Bind native destination page");
        Check(!navigation.gameObject.TryGetComponent<NovelUiActionComponent>(out _), "Page navigation creation left a competing story action component.");
        var collection = uiEdits.CreateList(container.gameObject);
        new NovelUiBindingEditService(new NativeProperties(scenes)).SetCollection(collection, "history");
        Check(runtime.interactions.history.Undo().succeeded && collection.collection == "choices"
            && runtime.interactions.history.Redo().succeeded && collection.collection == "history", "Collection selection bypassed native History.");
        var pager = uiEdits.CreateListNavigation(container.gameObject);
        scenes.ChangeProperty(pager, nameof(pager.target), () => pager.target = collection, "Bind native collection");
        Check(!pager.gameObject.TryGetComponent<NovelUiActionComponent>(out _), "List navigation creation left a competing action.");
        string uiPrefabPath = workspace.SavePrefab(canvas.gameObject, "Game");
        var canvasCopy = scenes.InstantiatePrefab(assets.Load<PrefabAsset>(AssetPath.Project(uiPrefabPath)), scene);
        var copiedTarget = canvasCopy.GetComponent<NovelTargetComponent>(); copiedTarget.targetId = "history/ui-copy";
        var copiedPage = canvasCopy.transform.children.Single().gameObject.GetComponent<NovelPageComponent>();
        var copiedContainer = copiedPage.transform.children.Single().gameObject;
        var copiedPager = copiedContainer.transform.children.SelectMany(static child => child.gameObject.GetComponents<NovelUiListNavigationComponent>()).Single();
        Check(copiedPager.target is { collection: "history" } copiedList && !ReferenceEquals(copiedList, collection)
            && ReferenceEquals(copiedList.transform.parent, copiedContainer.transform),
            "A native UI Prefab retained the original instance's list navigation target.");
        Check(copiedPage.initialFocus is { } copiedButton && !ReferenceEquals(copiedButton, uiButton.gameObject)
            && ReferenceEquals(copiedButton.transform.parent!.parent, copiedPage.transform)
            && copiedButton.GetComponent<NovelUiBindingComponent>().text == "dialogue.text",
            "A native UI Prefab instance retained another instance's focus reference.");
        canvasCopy.SetActive(false);
        var sceneAudio = authoring.CreateAudio(scene);
        scenes.ChangeProperty(sceneAudio, nameof(sceneAudio.gain), () => sceneAudio.gain = .4, "Set scene audio gain");
        Check(runtime.interactions.history.Undo().succeeded && sceneAudio.gain == 1
            && runtime.interactions.history.Redo().succeeded && sceneAudio.gain == .4, "Native audio gain did not use real property History.");
        scenes.ChangeProperty(sceneAudio, nameof(sceneAudio.playOnEnable), () => sceneAudio.playOnEnable = false, "Disable automatic audio");
        scenes.ChangeProperty(sceneAudio, nameof(sceneAudio.paused), () => sceneAudio.paused = true, "Set authored audio pause");
        var effect = (NovelScreenEffectComponent)scenes.AddComponent(item, typeof(NovelScreenEffectComponent));
        scenes.ChangeProperty(effect, nameof(effect.parameters), () => effect.parameters = new()
            { kind = Model.Performance.ScreenEffectKind.ColorGrade, brightness = .2, contrast = 1.3 }, "Override screen effect");
        Check(runtime.interactions.history.Undo().succeeded && effect.parameters is null
            && runtime.interactions.history.Redo().succeeded && effect.parameters?.brightness == .2,
            "Native effect override did not undo back to inherited parameters or redo detached values.");
        var cameraObject = scenes.CreateGameObject(scene);
        var camera = (NovelCameraComponent)scenes.AddComponent(cameraObject, typeof(NovelCameraComponent));
        scenes.ChangeProperty(camera, nameof(camera.followTarget), () => camera.followTarget = item, "Bind native follow target");
        scenes.ChangeProperty(camera, nameof(camera.following), () => camera.following = true, "Enable native camera follow");
        camera.ApplyFollow((owner, property, mutation) => scenes.ChangeProperty(owner, property, mutation, "Sample camera follow"));
        Check(camera.transform.worldPosition.x == 60 && runtime.interactions.history.Undo().succeeded && camera.transform.localPosition == Vector3.ZERO
            && runtime.interactions.history.Redo().succeeded && camera.transform.worldPosition.x == 60,
            "Explicit camera follow sampling did not use native Transform History.");
        var bindingSystem = (Integration.Hosting.NovelSceneSystem)scenes.AddSystem(scene, typeof(Integration.Hosting.NovelSceneSystem));
        authoring.BindSceneTargets(bindingSystem);
        Check(bindingSystem.targets.Length == 3 && runtime.interactions.history.Undo().succeeded && bindingSystem.targets.Length == 0
            && runtime.interactions.history.Redo().succeeded && bindingSystem.targets.Length == 3,
            "Explicit scene target references did not use native property Undo/Redo.");
        string path = workspace.Save(scene, "Game");
        Guid sceneId = scene.identity.persistentId;
        SceneManager.UnloadScene(scene);
        var reopened = workspace.Open(path);
        // Native SceneAsset instantiation allocates new identities and remaps component references.
        var reopenedOwner = NovelSceneCapture.Targets(reopened)["history/main"];
        Check(ReferenceEquals(reopened.GetSystems().OfType<Integration.Hosting.NovelSceneSystem>().Single().ResolveTarget<NovelImageComponent>("history/main").gameObject, reopenedOwner),
            "Native scene reopening did not remap the system's explicit component target references.");
        var reopenedPart = reopened.GetObjects().SelectMany(value => value.GetComponents<NovelCharacterPartComponent>()).Single();
        var reopenedPerformance = reopenedOwner.GetComponent<NovelPerformanceComponent>();
        var reopenedCanvas = NovelSceneCapture.Targets(reopened)["history/ui"].GetComponent<NovelUiRootComponent>();
        var reopenedPage = reopenedCanvas.transform.children.Single().gameObject.GetComponent<NovelPageComponent>();
        var reopenedAudio = reopened.GetObjects().SelectMany(static owner => owner.GetComponents<NovelAudioComponent>()).Single();
        Check(reopenedOwner.GetComponent<NovelScreenEffectComponent>().parameters is { brightness: .2, contrast: 1.3 },
            "Native scene save/reopen lost the instance-owned screen-effect parameters.");
        var reopenedCamera = reopened.GetObjects().SelectMany(static owner => owner.GetComponents<NovelCameraComponent>()).Single();
        Check(reopenedCamera.following && ReferenceEquals(reopenedCamera.followTarget, reopenedOwner),
            "Native scene reopening did not remap the camera's real follow target reference.");
        Check(reopenedAudio.gain == .4 && reopenedAudio.paused && !reopenedAudio.playOnEnable && !reopenedAudio.isPlaying,
            "Native scene persistence lost audio authoring properties or started a voice during edit reopening.");
        Check(reopenedCanvas.documentTemplate?.fonts.Count == 1 && reopenedPage.initialFocus?.GetComponent<NovelUiTextComponent>().text == "Continue"
            && reopenedPage.initialFocus?.transform.parent?.gameObject.GetComponent<NovelUiContainerComponent>().layout == Model.Interface.UiContainerLayout.Vertical,
            "Native UI scene save/reopen lost the imported engine document, real hierarchy or remapped focus reference.");
        Check(reopened.identity.persistentId != sceneId && reopenedOwner.identity.persistentId != objectId
            && ReferenceEquals(reopenedPart.character, reopenedOwner.GetComponent<NovelCharacterComponent>())
            && reopenedOwner.GetComponent<NovelImageComponent>().width == 100
            && reopenedPerformance.elapsed == .5 && ReferenceEquals(reopenedPerformance.bindings.Single().target, reopenedOwner)
            && NovelSceneCapture.Capture(reopened, view).items.Count == 2,
            "Native save/reopen lost a logical story target, remapped component reference or frame-producing property.");
    }
    private sealed class NativeProperties(SceneEdits scenes) : IInspectionPropertyEditService
    {
        public bool ChangeProperty(object owner, string propertyName, Action mutation, string historyName, string? mergeKey, string? componentName)
            => scenes.ChangeProperty((EngineObject)owner, propertyName, mutation, historyName, mergeKey);
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}

[EditorModule("vn.tests.native-scene-probe", order: 220)]
public sealed class NativeSceneHistoryProbe(IEditorSceneWorkspace? workspace = null) : EditorModule
{
    public IEditorSceneWorkspace? workspace { get; } = workspace;
}

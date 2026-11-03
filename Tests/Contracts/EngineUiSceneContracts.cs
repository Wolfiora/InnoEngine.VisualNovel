using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using Inno.Assets;
using Inno.Assets.Pipeline;
using Inno.Core.Diagnostics;
using Inno.Core.Logging;
using Inno.Core.Serialization;
using Inno.Extensibility.Modules;
using Inno.Core.Identity;
using Inno.Core.Mathematics;
using Inno.Extensibility.Types;
using Inno.Scene;
using Inno.UI;
using Inno.UI.Runtime;
using Inno.VisualNovel.Integration.Components.Interface;
using Inno.VisualNovel.Integration.Interface.Engine;
using Inno.VisualNovel.Integration.Hosting;
using Inno.VisualNovel.Integration.Rendering;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class EngineUiSceneContracts
{
    internal static void NativeBackendProducesGeometryWithoutDesktop(
        IdentityAllocator identities,
        TypeCatalog types,
        ModuleHost modules,
        SerializationRegistry serialization
    ) {
        string repository = Environment.CurrentDirectory;
        string output = Path.GetFullPath(AppContext.BaseDirectory);
        if (!output.StartsWith(Path.Combine(repository, "artifacts") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Native UI tests must write only to this repository's artifacts directory.");
        string source = Path.Combine(repository, "artifacts", "EngineEditorSnapshot", "native", "ui", "windows-x64", "inno-ui-debug.dll");
        string directory = Path.Combine(output, "native", "ui", "windows-x64"); Directory.CreateDirectory(directory);
        File.Copy(source, Path.Combine(directory, "inno-ui-debug.dll"), true);
        var deployment = new Inno.Native.LibraryLoading.NativeDeploymentIndex("windows-x64",
            new Dictionary<string, string> { ["inno-ui-debug"] = "ui/windows-x64/inno-ui-debug.dll" }, new Dictionary<string, string>());
        File.WriteAllBytes(Path.Combine(output, "native", Inno.Native.LibraryLoading.NativeDeploymentIndex.C_FILE_NAME), deployment.Encode());
        Console.WriteLine("Native UI module SHA256: " + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))));
        using var world = new SceneWorld(identities, types); using var scope = world.EnterScope();
        var scene = SceneManager.LoadNewSceneAdditive("Offscreen engine UI");
        var root = scene.CreateObject("Canvas").AddComponent<NovelUiRootComponent>(); root.width = 800; root.height = 600;
        var owner = scene.CreateObject("Button"); owner.transform.SetParent(root.transform);
        owner.AddComponent<NovelUiLayoutComponent>().size = new(180, 60);
        owner.AddComponent<NovelUiPanelComponent>().color = 0xFF0000FF;
        owner.AddComponent<NovelUiButtonComponent>(); owner.AddComponent<NovelUiActionComponent>().action = "advance";
        using var service = new UiRuntime(new Inno.Adapter.UI.RmlUi.RmlUiBackend(), new NoArtifacts()); service.Attach();
        using var surface = new NovelEngineUiSurface(root, service);
        UiRenderFrame frame = surface.Capture();
        Check(frame.commands.Count > 0 && frame.meshUpdates.Count > 0, "The real engine backend produced no first-frame geometry.");
        Check(surface.HasElementAtPoint(new(400, 300)), "The engine did not hit the projected native button.");
        _ = surface.Handle(new(new(400, 300), Vector2.ZERO, 0, [], [], [Inno.Core.Input.MouseButton.Left], [], []));
        var actions = surface.Handle(new(new(400, 300), Vector2.ZERO, 0, [], [], [], [Inno.Core.Input.MouseButton.Left], []));
        Check(actions.Count == 1 && actions[0].action == "advance", "Real engine pointer input did not activate the native button.");
        _ = surface.Handle(new(new(400, 300), Vector2.ZERO, 0, [Inno.Core.Input.KeyCode.Tab], [Inno.Core.Input.KeyCode.Tab], [], [], []));
        var keyboard = surface.Handle(new(new(400, 300), Vector2.ZERO, 0, [Inno.Core.Input.KeyCode.Enter], [Inno.Core.Input.KeyCode.Enter], [], [], []));
        Check(keyboard.Count == 1, "Engine keyboard navigation did not activate the scene button.");
        var overlay = scene.CreateObject("Pointer pass-through overlay"); overlay.transform.SetParent(root.transform);
        overlay.AddComponent<NovelUiLayoutComponent>().size = new(180, 60);
        var overlayPanel = overlay.AddComponent<NovelUiPanelComponent>(); overlayPanel.blocksPointer = false;
        _ = surface.Capture();
        _ = surface.Handle(new(new(400, 300), Vector2.ZERO, 0, [], [], [Inno.Core.Input.MouseButton.Left], [], []));
        Check(surface.Handle(new(new(400, 300), Vector2.ZERO, 0, [], [], [], [Inno.Core.Input.MouseButton.Left], [])).Count == 1,
            "The engine did not pass a pointer through an explicitly non-blocking native panel.");
        overlayPanel.blocksPointer = true; _ = surface.Capture();
        _ = surface.Handle(new(new(400, 300), Vector2.ZERO, 0, [], [], [Inno.Core.Input.MouseButton.Left], [], []));
        Check(surface.Handle(new(new(400, 300), Vector2.ZERO, 0, [], [], [], [Inno.Core.Input.MouseButton.Left], [])).Count == 0,
            "A blocking native panel allowed a control behind it to activate.");
        scene.DestroyObject(overlay);
        owner.SetActive(false);
        Check(surface.Capture().commands.Count == 0, "Deactivating the native button left engine-generated geometry.");
        string fixture = Path.Combine(repository, "artifacts", "AcceptanceProjects", "EngineUiFonts-" + Guid.NewGuid().ToString("N"));
        string assetsRoot = Path.Combine(fixture, "Assets");
        Directory.CreateDirectory(Path.Combine(assetsRoot, "BuiltIn", "Fonts"));
        File.Copy(Path.Combine(repository, "Assets", "BuiltIn", "SceneUi.rml"), Path.Combine(assetsRoot, "BuiltIn", "SceneUi.rml"));
        File.Copy(Path.Combine(repository, "Assets", "BuiltIn", "Fonts", "NotoSansCJKsc-Regular.otf"), Path.Combine(assetsRoot, "BuiltIn", "Fonts", "NotoSansCJKsc-Regular.otf"));
        NativeUiFontFixture.Prepare(assetsRoot);
        using var logs = new LogRouter();
        var diagnostics = new DiagnosticHub(); diagnostics.RegisterSink(new ReportDiagnostics());
        using var assets = new AssetPipeline(modules, types, serialization, identities, diagnostics, logs,
            AssetPipelineOptions.Create(assetsRoot, Path.Combine(fixture, "Library")) with { enableFileSystemWatcher = false });
        root.documentTemplate = assets.Load<UiDocumentAsset>(AssetPath.Project("BuiltIn/SceneUi.rml"));
        Check(root.documentTemplate.fonts.Count == 1, "The default engine UI template lost its imported font dependency.");
        owner.SetActive(true); owner.AddComponent<NovelUiTextComponent>().text = "中文 & <literal>";
        using var textService = new UiRuntime(new Inno.Adapter.UI.RmlUi.RmlUiBackend(), assets); textService.Attach();
        using var textSurface = new NovelEngineUiSurface(root, textService);
        var textFrame = textSurface.Capture();
        Check(textFrame.textureUpdates.Count > 0 && textFrame.commands.Any(command => command.texture.isValid),
            "The real engine UI did not generate font texture geometry for native text.");
        NativeUiFontFixture.Verify(root, owner.GetComponent<NovelUiTextComponent>(), textSurface, assets);
        owner.AddComponent<NovelUiControlStyleComponent>();
        foreach (var point in new[] { new Vector2(400, 280), new Vector2(400, 300) })
        {
            _ = textSurface.Capture();
            _ = textSurface.Handle(new(point, Vector2.ZERO, 0, [], [], [Inno.Core.Input.MouseButton.Left], [], []));
            Check(textSurface.Handle(new(point, Vector2.ZERO, 0, [], [], [], [Inno.Core.Input.MouseButton.Left], [])).Count == 1,
                "A styled button with an imported font lost a click on its text or background.");
        }
        owner.GetComponent<NovelUiTextComponent>().text = "日本語 English";
        Check(textSurface.Capture().commands.Count > 0, "Changing native text lost the engine document.");
        var dialogueBinding = owner.AddComponent<NovelUiBindingComponent>(); dialogueBinding.text = "dialogue.text";
        var richSource = new RichSource();
        var concealed = textSurface.Capture(richSource);
        Check(!concealed.commands.Any(command => command.texture.isValid), "Unrevealed rich text emitted visible glyph geometry.");
        richSource.visible = 1;
        Check(textSurface.Capture(richSource).commands.Any(command => command.texture.isValid), "A revealed grapheme did not produce engine glyph geometry.");
        richSource.visible = int.MaxValue;
        Check(textSurface.Capture(richSource).commands.Any(command => command.texture.isValid), "Ruby and emphasis failed to produce engine UI text.");
        dialogueBinding.text = string.Empty;
        using var uiScope = UiExecutionContext.EnterScope(textService);
        var system = scene.AddSystem<NovelSceneSystem>(); system.designWidth = 800; system.designHeight = 600;
        system.surfaceMaterial = new Inno.Rendering.Assets.MaterialAsset();
        var sceneFrame = system.CaptureScene();
        Check(sceneFrame.uiMeshes.Count > 0 && sceneFrame.uiMeshes.Any(mesh => mesh.texture.bitmap is not null) && !system.hasPresentation,
            "The production native scene path did not consume the engine UI frame without Play.");
        Check(system.CaptureScene().uiMeshes.Count > 0, "Incremental engine mesh resources were lost on repeated Scene/Game captures.");
        var effectOwner = scene.CreateObject("Full output effect");
        var effect = effectOwner.AddComponent<Inno.VisualNovel.Integration.Components.NovelScreenEffectComponent>();
        effect.preset = new() { resourceId = "native-ui-grade", kind = Inno.VisualNovel.Model.Performance.VisualEffectKind.Screen };
        effect.includeUi = true; system.imageEffectsMaterial = new Inno.Rendering.Assets.MaterialAsset();
        var processed = system.CaptureScene();
        Check(processed.uiMeshes.Count == 0 && processed.image is Inno.VisualNovel.Rendering.Frames.NovelProcessImage
            && processed.image.Meshes().Any(mesh => mesh.texture.bitmap is not null),
            "Actual engine UI geometry bypassed the scene's Include UI screen effect.");
        scene.DestroyObject(effectOwner);
        Check(ReferenceEquals(system.PickSceneUi(new(800, 600, Matrix.identity), new(400, 300)), owner),
            "Scene selection did not use the actual engine UI document hit.");
        owner.SetActive(false);
        Check(system.CaptureScene().uiMeshes.Count == 0, "The production Scene/Game path retained a deactivated UI object.");
        var toggleOwner = scene.CreateObject("Boolean setting"); toggleOwner.transform.SetParent(root.transform);
        toggleOwner.AddComponent<NovelUiLayoutComponent>().size = new(180, 60);
        var toggle = toggleOwner.AddComponent<NovelUiToggleComponent>();
        var binding = toggleOwner.AddComponent<NovelUiBindingComponent>(); binding.value = "setting";
        var values = new ToggleSource();
        _ = textSurface.Capture(values);
        _ = textSurface.Handle(new(new(400, 300), Vector2.ZERO, 0, [], [], [Inno.Core.Input.MouseButton.Left], [], []), values);
        _ = textSurface.Handle(new(new(400, 300), Vector2.ZERO, 0, [], [], [], [Inno.Core.Input.MouseButton.Left], []), values);
        Check(values.value && values.writes == 1 && !toggle.value, "Engine toggle activation did not write exactly once to its declared Boolean owner.");
        values.accept = false;
        _ = textSurface.Handle(new(new(400, 300), Vector2.ZERO, 0, [], [], [Inno.Core.Input.MouseButton.Left], [], []), values);
        _ = textSurface.Handle(new(new(400, 300), Vector2.ZERO, 0, [], [], [], [Inno.Core.Input.MouseButton.Left], []), values);
        Check(values.value && !toggle.value, "A rejected binding write changed the native toggle's authored fallback.");
        binding.value = string.Empty;
        _ = textSurface.Handle(new(new(400, 300), Vector2.ZERO, 0, [], [], [Inno.Core.Input.MouseButton.Left], [], []));
        _ = textSurface.Handle(new(new(400, 300), Vector2.ZERO, 0, [], [], [], [Inno.Core.Input.MouseButton.Left], []));
        Check(toggle.value, "An unbound toggle did not retain its actual component value.");
        toggleOwner.SetActive(false);
        var grid = scene.CreateObject("Grid"); grid.transform.SetParent(root.transform);
        grid.AddComponent<NovelUiLayoutComponent>().size = new(400, 200);
        var gridLayout = grid.AddComponent<NovelUiContainerComponent>(); gridLayout.layout = Inno.VisualNovel.Model.Interface.UiContainerLayout.Grid; gridLayout.columns = 2;
        var cells = new List<GameObject>();
        for (int i = 0; i < 3; i++)
        {
            var cell = scene.CreateObject("Cell " + i); cell.transform.SetParent(grid.transform); cells.Add(cell);
            var cellLayout = cell.AddComponent<NovelUiLayoutComponent>(); cellLayout.size = new(100, 50); cellLayout.widthMode = Inno.VisualNovel.Model.Interface.UiSizeMode.Fill;
            cell.AddComponent<NovelUiPanelComponent>(); cell.AddComponent<NovelUiButtonComponent>();
            var action = cell.AddComponent<NovelUiActionComponent>(); action.action = "choice"; action.argument = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        Check(Activate(300, 225) == "0" && Activate(500, 225) == "1" && Activate(300, 287) == "2",
            "Engine-managed grid cells did not follow native sibling order and row spacing.");
        cells[0].SetActive(false);
        Check(Activate(300, 225) == "1", "An inactive native grid child still occupied a layout cell.");
        grid.SetActive(false);
        var scroll = scene.CreateObject("Scroll"); scroll.transform.SetParent(root.transform);
        scroll.AddComponent<NovelUiLayoutComponent>().size = new(240, 100);
        var flow = scroll.AddComponent<NovelUiContainerComponent>(); flow.layout = Inno.VisualNovel.Model.Interface.UiContainerLayout.Vertical; flow.gap = 0;
        scroll.AddComponent<NovelUiScrollComponent>();
        for (int i = 0; i < 4; i++)
        {
            var row = scene.CreateObject("Scroll row " + i); row.transform.SetParent(scroll.transform);
            var size = row.AddComponent<NovelUiLayoutComponent>(); size.size = new(180, 60);
            row.AddComponent<NovelUiPanelComponent>(); row.AddComponent<NovelUiButtonComponent>();
            var action = row.AddComponent<NovelUiActionComponent>(); action.action = "choice"; action.argument = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        Check(Activate(350, 275) == "0", "The native scroll container did not start at its first child.");
        _ = textSurface.Handle(new(new(350, 275), new(0, 10), 0, [], [], [], [], []));
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < 300)
        { _ = textSurface.Handle(new(new(350, 275), Vector2.ZERO, 0, [], [], [], [], [])); System.Threading.Thread.Sleep(10); }
        Check(Activate(350, 275) != "0", "Engine wheel scrolling did not change the visible native child.");
        _ = textSurface.Handle(new(new(350, 380), Vector2.ZERO, 0, [], [], [Inno.Core.Input.MouseButton.Left], [], []));
        Check(textSurface.Handle(new(new(350, 380), Vector2.ZERO, 0, [], [], [], [Inno.Core.Input.MouseButton.Left], [])).Count == 0,
            "A scrolled child outside the real engine clip accepted input.");
        scroll.SetActive(false);
        world.ConfigurePrefabInstantiation(serialization, assets);
        var template = scene.CreateObject("Choice row template");
        template.AddComponent<NovelUiLayoutComponent>().size = new(200, 50);
        template.AddComponent<NovelUiPanelComponent>(); template.AddComponent<NovelUiButtonComponent>();
        var rowAction = template.AddComponent<NovelUiActionComponent>(); rowAction.action = "choose"; rowAction.argumentBinding = "item.id";
        template.AddComponent<NovelUiBindingComponent>().interactable = "item.enabled";
        var itemPrefab = PrefabAsset.Capture(template, serialization, assets);
        var listOwner = scene.CreateObject("Native choices"); listOwner.transform.SetParent(root.transform);
        listOwner.AddComponent<NovelUiLayoutComponent>().size = new(300, 200);
        var listFlow = listOwner.AddComponent<NovelUiContainerComponent>(); listFlow.layout = Inno.VisualNovel.Model.Interface.UiContainerLayout.Vertical; listFlow.gap = 0;
        var list = listOwner.AddComponent<NovelUiListComponent>(); list.collection = "choices"; list.itemPrefab = itemPrefab; list.capacity = 2;
        var rows = new RowSource(); list.Refresh(rows);
        var firstRow = list.transform.children[0].gameObject;
        Check(list.total == 3 && list.transform.children.Count == 2 && firstRow.isPartOfPrefabInstance,
            "A native collection did not instantiate a bounded window of real Prefab children.");
        _ = textSurface.Capture(rows);
        _ = textSurface.Handle(new(new(300, 225), Vector2.ZERO, 0, [], [], [Inno.Core.Input.MouseButton.Left], [], []), rows);
        var selected = textSurface.Handle(new(new(300, 225), Vector2.ZERO, 0, [], [], [], [Inno.Core.Input.MouseButton.Left], []), rows);
        Check(selected.Single().argument == "alpha", "The actual engine row button did not bind its own stable choice key.");
        rows.reverse = true; list.Refresh(rows);
        Check(ReferenceEquals(list.transform.children[1].gameObject, firstRow), "Reordering a keyed native list replaced an existing item's object identity.");
        rows.duplicate = true;
        try { list.Refresh(rows); throw new InvalidOperationException("Duplicate native list keys were accepted."); }
        catch (InvalidOperationException error) when (error.Message.Contains("unique", StringComparison.Ordinal)) { }
        Check(ReferenceEquals(list.transform.children[1].gameObject, firstRow), "Invalid collection data destroyed the last valid native rows.");
        rows.duplicate = false; list.first = 2; list.Refresh(rows);
        Check(list.transform.children.Count == 1 && firstRow.isDestroyed, "Paging failed to retire native row objects outside the visible window.");
        var pagerOwner = scene.CreateObject("List pager"); pagerOwner.transform.SetParent(root.transform);
        pagerOwner.transform.localPosition = new(200, -150, 0);
        pagerOwner.AddComponent<NovelUiLayoutComponent>().size = new(180, 60);
        pagerOwner.AddComponent<NovelUiButtonComponent>(); pagerOwner.AddComponent<NovelUiControlStyleComponent>();
        pagerOwner.AddComponent<NovelUiTextComponent>().text = "Next page";
        var pager = pagerOwner.AddComponent<NovelUiListNavigationComponent>(); pager.target = list;
        Check(!pager.canNavigate && !Navigate(), "The last native list page accepted Next navigation.");
        pager.operation = NovelUiListNavigation.First;
        Check(Navigate() && list.first == 0, "Native first-page navigation did not update the actual list offset.");
        list.Refresh(rows);
        pager.operation = NovelUiListNavigation.Next;
        Check(Navigate() && list.first == 2, "Native next-page navigation ignored the list capacity.");
        list.Refresh(rows);
        pager.operation = NovelUiListNavigation.Previous;
        Check(Navigate() && list.first == 0, "Native previous-page navigation failed.");
        pager.operation = NovelUiListNavigation.Last;
        Check(Navigate() && list.first == 2, "Native last-page navigation did not retain the partial final page.");
        list.enabled = false;
        Check(!pager.canNavigate && !Navigate(), "A disabled native list accepted navigation.");
        list.enabled = true; list.Refresh(rows);
        pager.target = null;
        Check(!Navigate() && list.first == 2, "A missing list destination mutated a previous target.");
        scene.DestroyObject(pagerOwner);
        var decoration = scene.CreateObject("Authored list decoration"); decoration.transform.SetParent(list.transform);
        list.Clear(); Check(!decoration.isDestroyed && list.transform.children.Count == 1, "Clearing generated rows destroyed an author's unrelated child.");
        list.first = 0; list.Refresh(rows);
        world.FixedUpdate(0);
        system.StopGame();
        Check(!decoration.isDestroyed && list.transform.children.Count == 1 && list.total == 0,
            "Stopping the owning scene system left generated rows active or removed authored decoration.");
        NativeUiFocusFixture.Verify(scene, root, textService);
        root.gameObject.SetActive(false);
        try { NativeUiRoutingFixture.Verify(scene, system); }
        finally { system.StopGame(); }
        NativeUiRuntimeFixture.Verify();

        bool Navigate()
        {
            _ = textSurface.Capture(rows);
            _ = textSurface.Handle(new(new(600, 450), Vector2.ZERO, 0, [], [], [Inno.Core.Input.MouseButton.Left], [], []), rows);
            var requests = textSurface.Handle(new(new(600, 450), Vector2.ZERO, 0, [], [], [], [Inno.Core.Input.MouseButton.Left], []), rows);
            foreach (var request in requests) system.InvokeSceneAction(request);
            return requests.Count == 1;
        }

        string Activate(float x, float y)
        {
            _ = textSurface.Capture();
            _ = textSurface.Handle(new(new(x, y), Vector2.ZERO, 0, [], [], [Inno.Core.Input.MouseButton.Left], [], []));
            return textSurface.Handle(new(new(x, y), Vector2.ZERO, 0, [], [], [], [Inno.Core.Input.MouseButton.Left], [])).Single().argument;
        }
    }

    internal static void EngineOwnsInteractionAndNativeObjectsOwnContent(
        IdentityAllocator identities,
        TypeCatalog types
    ) {
        using var world = new SceneWorld(identities, types); using var scope = world.EnterScope();
        var scene = SceneManager.LoadNewSceneAdditive("Engine UI scene");
        var root = scene.CreateObject("Canvas").AddComponent<NovelUiRootComponent>(); root.width = 800; root.height = 600;
        var owner = scene.CreateObject("Button"); owner.transform.SetParent(root.transform);
        var button = owner.AddComponent<NovelUiButtonComponent>();
        owner.AddComponent<NovelUiPanelComponent>();
        owner.AddComponent<NovelUiActionComponent>().action = "advance";
        owner.AddComponent<NovelUiLayoutComponent>().size = new(180, 60);
        var child = scene.CreateObject("Decoration"); child.transform.SetParent(owner.transform);
        var backend = new ObservingBackend();
        using var service = new UiRuntime(backend, new NoArtifacts()); service.Attach();
        using var surface = new NovelEngineUiSurface(root, service);
        surface.Capture();
        string id = Id(owner), childId = Id(child);
        Check(backend.fragment.Contains(id, StringComparison.Ordinal) && backend.fragment.Contains(childId, StringComparison.Ordinal),
            "The engine document did not derive its elements from native object identity.");
        Check(backend.updates == 1 && backend.renders == 1 && backend.lastInput is { keysPressed.Count: 0, buttonsPressed.Count: 0, textInput.Count: 0 }, "Authoring capture processed player input.");
        int replacements = backend.replacements, attributes = backend.attributes.Count;
        surface.Capture();
        Check(backend.replacements == replacements && backend.attributes.Count == attributes, "Unchanged frames rebuilt the engine document or reset its focus.");
        owner.transform.localPosition = new(45, 25, 0);
        surface.Capture();
        Check(backend.replacements == replacements && backend.attributes.Last().value.Contains("45px,-25px", StringComparison.Ordinal),
            "Transform editing rebuilt the structure or ignored the native position.");
        int layoutUpdates = backend.updates;
        backend.events.Add(new(UiEventType.Click, new(999), id));
        backend.events.Add(new(UiEventType.Click, backend.document, childId));
        backend.events.Add(new(UiEventType.Click, backend.document, id));
        var input = new UiInputSnapshot(new(400, 300), Vector2.ZERO, 0, [], [], [], [], []);
        var actions = surface.Handle(input);
        Check(backend.updates == layoutUpdates + 1 && ReferenceEquals(backend.lastInput, input) && actions.Count == 1 && actions[0].action == "advance",
            "Input bypassed the engine service, accepted a foreign document, or duplicated a bubbled activation.");
        var palette = owner.AddComponent<NovelUiControlStyleComponent>(); palette.hover = 0x123456FF; palette.pressed = 0x654321FF; palette.focus = 0xABCDEFff;
        backend.events.Add(new(UiEventType.MouseEnter, backend.document, id));
        backend.events.Add(new(UiEventType.Focus, backend.document, id));
        _ = surface.Handle(input);
        Check(backend.attributes.Last().value.Contains("background-color:#123456FF", StringComparison.Ordinal)
            && backend.attributes.Last().value.Contains("border-color:#ABCDEFFF", StringComparison.Ordinal), "Engine hover and focus events did not apply the actual component palette.");
        _ = surface.Handle(new(new(400, 300), Vector2.ZERO, 0, [], [], [Inno.Core.Input.MouseButton.Left], [], []));
        Check(backend.attributes.Last().value.Contains("background-color:#654321FF", StringComparison.Ordinal), "A held pointer did not apply pressed feedback.");
        _ = surface.Handle(new(new(400, 300), Vector2.ZERO, 0, [], [], [], [Inno.Core.Input.MouseButton.Left], []));
        button.interactable = false;
        backend.events.Add(new(UiEventType.Click, backend.document, id));
        Check(surface.Handle(input).Count == 0, "A disabled native button accepted a queued engine event.");
        Check(backend.attributes.Last().value.Contains("background-color:#242630FF", StringComparison.Ordinal)
            && backend.attributes.Last().value.Contains("border-color:#00000000", StringComparison.Ordinal), "Disabled feedback retained active focus or ignored the native palette.");
        button.interactable = true;
        scene.DestroyObject(owner);
        backend.events.Add(new(UiEventType.Click, backend.document, id));
        Check(surface.Handle(input).Count == 0 && !backend.fragment.Contains(id, StringComparison.Ordinal), "Deleted native objects retained engine UI or stale input ownership.");
        root.gameObject.SetActive(false); surface.Capture();
        Check(backend.hides == 1 && !surface.HasElementAtPoint(new(0, 0)), "An inactive canvas retained its engine document.");
        root.gameObject.SetActive(true); surface.Capture();
        Check(backend.shows == 2, "Reactivation did not restore the engine document.");
        surface.Dispose(); surface.Dispose();
        Check(backend.destroyed == 1, "The engine UI context was not released exactly once.");
    }

    internal static void CandidateFailureDoesNotReplaceTheDocument(
        IdentityAllocator identities,
        TypeCatalog types
    ) {
        using var world = new SceneWorld(identities, types); using var scope = world.EnterScope();
        var scene = SceneManager.LoadNewSceneAdditive("UI candidate");
        var root = scene.CreateObject("Canvas").AddComponent<NovelUiRootComponent>();
        var owner = scene.CreateObject("Panel"); owner.transform.SetParent(root.transform); owner.AddComponent<NovelUiPanelComponent>();
        var backend = new ObservingBackend();
        using var service = new UiRuntime(backend, new NoArtifacts()); service.Attach();
        using var surface = new NovelEngineUiSurface(root, service); surface.Capture();
        int changes = backend.replacements;
        var slider = owner.AddComponent<NovelUiSliderComponent>();
        bool rejected = false;
        try { surface.Capture(); } catch (NotSupportedException error) { rejected = error.Message.Contains("Change", StringComparison.Ordinal); }
        Check(rejected && backend.replacements == changes, "Unsupported value controls silently used a parallel input implementation or damaged the valid document.");
        scene.DestroyObject(owner); surface.Capture();
        var before = backend.document;
        root.contentElement = "missing";
        try { surface.Capture(); throw new InvalidOperationException("Missing content slot was accepted."); }
        catch (InvalidOperationException error) when (error.Message.Contains("missing its scene content", StringComparison.Ordinal)) { }
        Check(backend.closed.Contains(backend.document) && !backend.closed.Contains(before), "Candidate failure closed the last valid UI document.");
    }

    private static string Id(GameObject owner) => "vn-" + owner.identity.persistentId.ToString("N");
    private sealed class ToggleSource : Inno.VisualNovel.UI.Binding.UiDataSource
    {
        internal bool value, accept = true;
        internal int writes;
        public override bool TryRead(string path, out Inno.VisualNovel.Model.Values.NovelValue result)
        { result = Inno.VisualNovel.Model.Values.NovelValue.Boolean(value); return path == "setting"; }
        public override bool TryWrite(string path, Inno.VisualNovel.Model.Values.NovelValue result)
        {
            if (!accept || path != "setting") return false;
            value = result.boolean; writes++; return true;
        }
    }
    private sealed class RichSource : Inno.VisualNovel.UI.Binding.UiDataSource
    {
        internal int visible;
        private readonly Inno.VisualNovel.Model.Text.NovelTextDocument m_document = Inno.VisualNovel.Model.Text.NovelTextMarkup.Parse(
            "[color=#FFCC88]e\u0301[/color] [b]English[/b] [ruby=ちゅう]中[/ruby] <button>& literal");
        public override bool TryRead(string path, out Inno.VisualNovel.Model.Values.NovelValue value)
        { value = Inno.VisualNovel.Model.Values.NovelValue.String(m_document.Prefix(visible)); return path == "dialogue.text"; }
        public override Inno.VisualNovel.UI.Text.UiRichText? ReadRichText(string path, Inno.VisualNovel.UI.Binding.UiDataRow? row)
            => path == "dialogue.text" ? new(m_document, visible) : null;
        public override bool TryWrite(string path, Inno.VisualNovel.Model.Values.NovelValue value) => false;
    }
    private sealed class RowSource : Inno.VisualNovel.UI.Binding.UiDataSource
    {
        internal bool reverse, duplicate;
        public override IReadOnlyList<Inno.VisualNovel.UI.Binding.UiDataRow> ReadRows(string path)
        {
            string[] keys = duplicate ? ["alpha", "alpha", "gamma"] : reverse ? ["beta", "alpha", "gamma"] : ["alpha", "beta", "gamma"];
            return keys.Select(key => new Inno.VisualNovel.UI.Binding.UiDataRow(key,
                new Dictionary<string, Inno.VisualNovel.Model.Values.NovelValue>
                {
                    ["id"] = Inno.VisualNovel.Model.Values.NovelValue.String(key),
                    ["enabled"] = Inno.VisualNovel.Model.Values.NovelValue.Boolean(true)
                })).ToArray();
        }
        public override bool TryRead(string path, out Inno.VisualNovel.Model.Values.NovelValue value) { value = new(); return false; }
        public override bool TryWrite(string path, Inno.VisualNovel.Model.Values.NovelValue value) => false;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class NoArtifacts : IAssetArtifactLookup
    {
        public ArtifactLease AcquireArtifact(Guid persistentId, string outputName) => throw new InvalidOperationException("This fixture has no fonts.");
        public bool TryGetArtifact(Guid persistentId, string outputName, out AssetArtifactInfo? artifact) { artifact = null; return false; }
    }
    private sealed class ReportDiagnostics : IDiagnosticSink
    {
        public void Replace(DiagnosticReport report) { foreach (var item in report.diagnostics) Console.WriteLine(item.code + ": " + item.message); }
        public void Clear(DiagnosticSource source) { }
    }
    private sealed class ObservingBackend : IUiBackend
    {
        public string implementationId => "tests.engine-ui";
        public UiBackendCapabilities capabilities { get; } = new([new("inno.ui-language.rml")], true);
        internal string fragment = string.Empty;
        internal int replacements, updates, renders, hides, shows, destroyed;
        internal UiDocumentHandle document;
        internal UiInputSnapshot? lastInput;
        internal readonly List<UiEvent> events = [];
        internal readonly List<(string id, string value)> attributes = [];
        internal readonly HashSet<UiDocumentHandle> closed = [];
        public UiContextHandle CreateContext(UiContextOptions options) => new(1);
        public void DestroyContext(UiContextHandle context) => destroyed++;
        public void SetViewport(UiContextHandle context, int width, int height, float density) { }
        public UiDocumentHandle LoadDocument(UiContextHandle context, UiDocumentSource source) { document = new(document.value + 1); return document; }
        public void ShowDocument(UiContextHandle context, UiDocumentHandle document) => shows++;
        public void HideDocument(UiContextHandle context, UiDocumentHandle document) => hides++;
        public void CloseDocument(UiContextHandle context, UiDocumentHandle document) => closed.Add(document);
        public bool SetText(UiContextHandle context, UiDocumentHandle document, string elementId, string text) => true;
        public bool SetContent(UiContextHandle context, UiDocumentHandle document, string elementId, UiDocumentFragment content)
        {
            if (elementId != "vn-scene") return false;
            fragment = content.text; replacements++; return true;
        }
        public bool SetAttribute(UiContextHandle context, UiDocumentHandle document, string elementId, string name, string value)
        { attributes.Add((elementId, value)); return true; }
        public bool SetClass(UiContextHandle context, UiDocumentHandle document, string elementId, string className, bool active) => true;
        public void RegisterFont(UiFontRegistration registration) { }
        public void RegisterTexture(UiContextHandle context, string source, UiTextureData texture) { }
        public bool HasElementAtPoint(UiContextHandle context, Vector2 position) => true;
        public void Update(UiContextHandle context, UiInputSnapshot input) { lastInput = input; updates++; }
        public UiRenderFrame Render(UiContextHandle context) { renders++; return UiRenderFrame.empty; }
        public IReadOnlyList<UiEvent> DrainEvents(UiContextHandle context) { var result = events.ToArray(); events.Clear(); return result; }
        public void Dispose() { }
    }
}

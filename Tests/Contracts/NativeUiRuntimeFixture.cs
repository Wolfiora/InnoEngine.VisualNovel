using System;
using System.Collections.Generic;
using System.Linq;
using Inno.Adapter.Modules.DotNet;
using Inno.Adapter.Serialization.DotNet;
using Inno.Core.Input;
using Inno.Extensibility.Types;
using Inno.Input;
using Inno.References;
using Inno.Rendering.Assets;
using Inno.Rendering.Runtime;
using Inno.Runtime;
using Inno.Scene;
using Inno.VisualNovel.Integration.Components.Interface;
using Inno.VisualNovel.Integration.Hosting;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class NativeUiRuntimeFixture
{
    internal static void Verify()
    {
        using var host = new EngineHostBuilder().UseMetadataSources(new DotNetAssemblyCatalogSource(typeof(NativeUiRuntimeFixture).Assembly),
            new ReflectionTypeCatalogSource(), new ReflectionSerializationMetadataSource()).Build();
        _ = host.modules.Register("vn.native-ui-runtime", [typeof(NovelSceneSystem).Assembly, typeof(Model.Story.StoryAsset).Assembly,
            typeof(Player.Sessions.NovelSession).Assembly, typeof(UI.Documents.UiDocumentInstance).Assembly,
            typeof(Stage.Composition.StageController).Assembly, typeof(Narrative.Execution.StoryMachine).Assembly]);
        using var session = host.CreateSession(new RuntimeSessionOptions { kind = RuntimeSessionKind.Play,
            applicationId = "inno.vn.native-ui-contract", jobExecutionMode = RuntimeJobExecutionMode.SingleThread });
        using var execution = session.EnterExecutionScope();
        using var inputScope = InputExecutionContext.EnterScope(new NoInput());
        var scene = SceneManager.LoadNewSceneAdditive("Native UI runtime");
        var system = scene.AddSystem<NovelSceneSystem>(); system.designWidth = 800; system.designHeight = 600; system.surfaceMaterial = new MaterialAsset();
        var canvas = scene.CreateObject("Canvas").AddComponent<NovelUiRootComponent>(); canvas.width = 800; canvas.height = 600;
        var page = Page("Title"); var modal = Page("Confirmation"); modal.modal = true;
        var open = Button(page, modal, NovelUiPageNavigation.Show);
        var close = Button(modal, modal, NovelUiPageNavigation.Close);
        modal.gameObject.SetActive(false);
        session.Tick(.01f);
        Check(system.hasPresentation && !modal.gameObject.activeSelf, "A native scene with no story failed to prepare its runtime UI frame.");
        using var content = new ContentReadScope([scene.identity], scene.identity.persistentId);
        ulong frame = 0;
        Queue(true, false);
        session.Tick(.01f);
        Queue(false, true);
        Check(!modal.gameObject.activeSelf, "Rendering dispatched a native page action before the scene update.");
        session.Tick(.01f);
        Check(modal.gameObject.activeSelf && page.gameObject.activeSelf && system.sceneUiError.Length == 0,
            "A no-script native button failed to show its explicitly referenced modal page: " + system.sceneUiError);
        Queue(true, false); session.Tick(.01f); Queue(false, true); session.Tick(.01f);
        Check(!modal.gameObject.activeSelf && !close.gameObject.isDestroyed, "Close Page destroyed authored controls or failed to remove the modal.");
        open.operation = NovelUiPageNavigation.Replace;
        Queue(true, false); session.Tick(.01f); Queue(false, true); session.Tick(.01f);
        Check(!page.gameObject.activeSelf && modal.gameObject.activeSelf && !open.gameObject.isDestroyed,
            "Replace Page did not change the actual native page activation.");
        modal.gameObject.SetActive(false); page.gameObject.SetActive(true);
        scene.DestroyObject(modal.gameObject);
        Queue(true, false); session.Tick(.01f); Queue(false, true); session.Tick(.01f);
        Check(page.gameObject.activeSelf && system.sceneUiError.Contains("existing page", StringComparison.Ordinal),
            "A missing destination partially closed the source page or failed silently.");
        system.StopGame(); session.Tick(.01f);
        Check(!system.hasPresentation, "A stopped native UI scene prepared a new runtime frame.");

        void Queue(bool press, bool release)
        {
            var output = new RenderOutputSession("test", content, new(0, 0, 1600, 1200), ++frame, .01f, new EmptyContent(),
                new(new(800, 600), true, default, 0, [], [], press ? [MouseButton.Left] : [], release ? [MouseButton.Left] : [], []));
            _ = system.Prepare(output); _ = system.Prepare(output);
        }
        NovelPageComponent Page(string name)
        {
            var owner = scene.CreateObject(name); owner.transform.SetParent(canvas.transform);
            owner.AddComponent<NovelUiLayoutComponent>().size = new(800, 600); return owner.AddComponent<NovelPageComponent>();
        }
        NovelUiPageLinkComponent Button(NovelPageComponent parent, NovelPageComponent target, NovelUiPageNavigation operation)
        {
            var owner = scene.CreateObject("Navigate"); owner.transform.SetParent(parent.transform);
            owner.AddComponent<NovelUiLayoutComponent>().size = new(180, 60); owner.AddComponent<NovelUiPanelComponent>(); owner.AddComponent<NovelUiButtonComponent>();
            var link = owner.AddComponent<NovelUiPageLinkComponent>(); link.target = target; link.operation = operation; return link;
        }
    }
    private sealed class NoInput : IInputService { public InputSnapshot snapshot => InputSnapshot.empty; }
    private sealed class EmptyContent : IViewContentCollector { public IReadOnlyList<ViewContentItem> Collect(ViewContentContext context) => []; }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

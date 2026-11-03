using System;
using System.Linq;
using Inno.Core.Input;
using Inno.Core.Mathematics;
using Inno.Scene;
using Inno.UI;
using Inno.VisualNovel.Integration.Components.Interface;
using Inno.VisualNovel.Integration.Hosting;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class NativeUiRoutingFixture
{
    internal static void Verify(GameScene scene, NovelSceneSystem system)
    {
        var bottom = Canvas("Bottom", 0); var top = Canvas("Top", 10);
        var first = Button(bottom, "bottom"); var second = Button(top, "top");
        Check(Click(400, 300) == "top", "Overlapping native canvases dispatched one click to the wrong owner.");
        second.SetActive(false);
        Check(Click(400, 300) == "bottom", "An empty front canvas prevented input reaching a real lower control.");
        second.SetActive(true);
        Check(Click(400, 300) == "top", "Reactivated front canvas did not regain hit ownership.");
        var modalOwner = scene.CreateObject("Modal page"); modalOwner.transform.SetParent(top.transform);
        modalOwner.AddComponent<NovelUiLayoutComponent>().size = new(30, 30);
        modalOwner.transform.localPosition = new(250, 200, 0);
        modalOwner.AddComponent<NovelPageComponent>().modal = true;
        Check(Click(400, 300) is null, "A modal on another part of the canvas allowed a lower canvas click.");
        modalOwner.SetActive(false);
        _ = system.HandleSceneInput(Input(400, 300, press: true));
        top.gameObject.SetActive(false);
        Check(system.HandleSceneInput(Input(400, 300, release: true)).actions.Count == 0,
            "Deleting or deactivating a press owner sent its release to the newly exposed canvas.");
        Check(Click(400, 300) == "bottom", "The abandoned press suppressed a subsequent independent click.");
        _ = system.HandleSceneInput(Input(400, 300, press: true));
        system.ReleaseSceneInput();
        Check(system.HandleSceneInput(Input(400, 300, release: true)).actions.Count == 0, "Focus loss retained pointer activation.");
        Check(Click(400, 300) == "bottom", "Focus-loss reset prevented future native controls from working.");
        bottom.transform.localPosition = new(50, 40, 0);
        bottom.transform.localRotation = Quaternion.FromEulerAnglesXYZDegrees(new(0, 0, 90));
        first.transform.localPosition = new(120, 0, 0);
        Vector3 nativePosition = first.transform.worldPosition;
        Check(Click(400 + nativePosition.x, 300 - nativePosition.y) == "bottom",
            "Native canvas rotation and translation were not inverted for engine hit testing: " + nativePosition);
        var page = scene.CreateObject("Keyboard page"); page.transform.SetParent(bottom.transform);
        page.AddComponent<NovelUiLayoutComponent>().size = new(800, 600);
        first.transform.SetParent(page.transform); page.AddComponent<NovelPageComponent>().initialFocus = first;
        system.ReleaseSceneInput();
        Check(system.HandleSceneInput(new(new(-1000, -1000), Vector2.ZERO, 0, [KeyCode.Enter], [KeyCode.Enter], [], [], []))
            .actions.Single().action == "bottom", "Keyboard-only input had no eligible native canvas owner.");
        bottom.gameObject.SetActive(false);
        Check(!system.HandleSceneInput(Input(400, 300)).consumed, "Inactive canvases consumed story or other-plugin input.");

        NovelUiRootComponent Canvas(string name, int order)
        {
            var owner = scene.CreateObject(name); var root = owner.AddComponent<NovelUiRootComponent>();
            root.width = 800; root.height = 600; root.order = order; return root;
        }
        GameObject Button(NovelUiRootComponent root, string action)
        {
            var owner = scene.CreateObject(action); owner.transform.SetParent(root.transform);
            owner.AddComponent<NovelUiLayoutComponent>().size = new(180, 60); owner.AddComponent<NovelUiPanelComponent>();
            owner.AddComponent<NovelUiButtonComponent>(); owner.AddComponent<NovelUiActionComponent>().action = action; return owner;
        }
        string? Click(float x, float y)
        {
            _ = system.HandleSceneInput(Input(x, y, press: true));
            return system.HandleSceneInput(Input(x, y, release: true)).actions.SingleOrDefault()?.action;
        }
    }
    private static UiInputSnapshot Input(float x, float y, bool press = false, bool release = false)
        => new(new(x, y), Vector2.ZERO, 0, [], [], press ? [MouseButton.Left] : [], release ? [MouseButton.Left] : [], []);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

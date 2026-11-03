using System;
using System.Linq;
using Inno.Core.Input;
using Inno.Core.Mathematics;
using Inno.Scene;
using Inno.UI;
using Inno.VisualNovel.Integration.Components.Interface;
using Inno.VisualNovel.Integration.Interface.Engine;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class NativeUiFocusFixture
{
    internal static void Verify(
        GameScene scene,
        NovelUiRootComponent root,
        IUiService service
    ) {
        foreach (var child in root.transform.children.ToArray()) child.gameObject.SetActive(false);
        var pageObject = scene.CreateObject("Default focus page"); pageObject.transform.SetParent(root.transform);
        pageObject.AddComponent<NovelUiLayoutComponent>().size = new(800, 600);
        var page = pageObject.AddComponent<NovelPageComponent>();
        var first = Button("first", -120);
        var second = Button("second", 120);
        page.initialFocus = second;
        using var surface = new NovelEngineUiSurface(root, service);
        _ = surface.Capture();
        Check(Confirm() == "second", "Engine document autofocus did not focus the native page's explicit second button.");
        _ = surface.Handle(new(new(-500, -500), Vector2.ZERO, 0, [KeyCode.Tab], [KeyCode.Tab], [], [], []));
        first.GetComponent<NovelUiPanelComponent>().color = 0x557799FF;
        _ = surface.Capture();
        Check(Confirm() == "first", "A style-only native update stole keyboard focus from the engine's Tab selection.");
        var alternateObject = scene.CreateObject("Alternate page"); alternateObject.transform.SetParent(root.transform);
        alternateObject.AddComponent<NovelUiLayoutComponent>().size = new(800, 600);
        var alternate = alternateObject.AddComponent<NovelPageComponent>();
        var third = Button("third", 0); third.transform.SetParent(alternateObject.transform); alternate.initialFocus = third;
        pageObject.SetActive(false); _ = surface.Capture();
        Check(Confirm() == "third", "Entering another native page failed to apply its initial focus.");
        alternateObject.SetActive(false); pageObject.SetActive(true); _ = surface.Capture();
        Check(Confirm() == "first", "Returning to a native page lost its engine-observed focus and forced the authored default.");
        var added = Button("new row", 250); _ = surface.Capture();
        Check(Confirm() == "first", "An unrelated native hierarchy change lost the live page's remembered focus.");
        scene.DestroyObject(added);
        page.initialFocus = first;
        _ = surface.Capture();
        Check(Confirm() == "first", "Changing the native initial-focus reference did not apply through the document lifecycle.");
        page.initialFocus = second; second.GetComponent<NovelUiButtonComponent>().interactable = false;
        _ = surface.Capture();
        Check(surface.Handle(new(new(-500, -500), Vector2.ZERO, 0, [KeyCode.Enter], [KeyCode.Enter], [], [], [])).Count == 0,
            "A disabled initial-focus control received an engine action.");
        second.GetComponent<NovelUiButtonComponent>().interactable = true; _ = surface.Capture();
        Check(Confirm() == "second", "Re-enabling the initial control did not restore engine-owned focus.");
        var outside = scene.CreateObject("Outside page"); outside.AddComponent<NovelUiButtonComponent>(); page.initialFocus = outside;
        bool invalid = false;
        try { _ = surface.Capture(); } catch (InvalidOperationException) { invalid = true; }
        Check(invalid, "A page accepted an initial-focus object outside its own native subtree.");
        page.initialFocus = second; _ = surface.Capture();
        Check(Confirm() == "second", "Invalid focus preparation destroyed the previously valid engine document.");
        pageObject.SetActive(false); _ = surface.Capture();
        Check(surface.Handle(new(new(-500, -500), Vector2.ZERO, 0, [KeyCode.Enter], [KeyCode.Enter], [], [], [])).Count == 0,
            "An inactive page retained a live focused control.");

        GameObject Button(string action, float x)
        {
            var owner = scene.CreateObject(action); owner.transform.SetParent(pageObject.transform); owner.transform.localPosition = new(x, 0, 0);
            owner.AddComponent<NovelUiLayoutComponent>().size = new(180, 60); owner.AddComponent<NovelUiPanelComponent>();
            owner.AddComponent<NovelUiButtonComponent>(); owner.AddComponent<NovelUiActionComponent>().action = action;
            return owner;
        }
        string Confirm() => surface.Handle(new(new(-500, -500), Vector2.ZERO, 0, [KeyCode.Enter], [KeyCode.Enter], [], [], [])).Single().action;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

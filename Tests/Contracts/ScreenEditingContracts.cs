using System;
using System.Linq;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Model.Interface;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class ScreenEditingContracts
{
    internal static void TreeOperations()
    {
        var page = new ScreenAsset { root = new() { id = "root", kind = UiElementKind.Panel } };
        string group = ScreenEditService.Add(page, "root", UiElementKind.Vertical);
        string child = ScreenEditService.Add(page, group, UiElementKind.Button);
        string clone = ScreenEditService.Duplicate(page, group);
        var original = ScreenEditService.Find(page, group);
        var copied = ScreenEditService.Find(page, clone);
        Check(original.children[0].id != copied.children[0].id && original.children[0].id == child, "Subtree copying reused internal identities.");
        copied.children[0].style.fontSize = 99;
        Check(original.children[0].style.fontSize != 99, "Subtree copying shared style storage.");
        string before = string.Join("|", ScreenEditService.Flatten(page).Select(static row => row.element.id));
        bool rejected = false;
        try { ScreenEditService.Reparent(page, group, child, 0); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && string.Join("|", ScreenEditService.Flatten(page).Select(static row => row.element.id)) == before, "Rejected tree cycle changed the page.");
        ScreenEditService.Reparent(page, child, "root", 1);
        Check(page.root.children[1].id == child && original.children.Length == 0, "Explicit reparenting changed identity or retained duplicate ownership.");
        ScreenEditService.Remove(page, clone);
        Check(ScreenEditService.Flatten(page).Count == 3, "Subtree deletion retained descendants.");
        page.initialFocus = child;
        ScreenEditService.Find(page, child).layout.x = 23;
        ScreenEditService.Find(page, child).tooltipBinding = "item.reason";
        ScreenEditService.Reparent(page, child, "root", 0);
        Check(page.root.children[0].id == child && page.initialFocus == child && ScreenEditService.Find(page, child).layout.x == 23
            && ScreenEditService.Find(page, child).tooltipBinding == "item.reason", "Sibling reordering guessed new identity, focus, geometry or bindings.");
        ScreenEditService.Reparent(page, child, "root", 1);
        Check(page.root.children[1].id == child && ScreenEditService.ReparentTargets(page, "root").Count == 0,
            "Moving forward in a sibling list used the pre-removal index or allowed moving the root.");

        var deep = new ScreenAsset(); deep.root.id = "depth0";
        UiElementDefinition cursor = deep.root;
        for (int index = 1; index <= 64; index++)
        { var next = new UiElementDefinition { id = "depth" + index }; cursor.children = [next]; cursor = next; }
        var branch = new UiElementDefinition { id = "branch", children = [new() { id = "leaf" }] };
        deep.root.children = [.. deep.root.children, branch];
        var targets = ScreenEditService.ReparentTargets(deep, "branch");
        Check(targets.Contains("depth62") && !targets.Contains("depth63") && !targets.Contains("branch") && !targets.Contains("leaf"),
            "Hierarchy choices included descendants or destinations outside the actual subtree depth budget.");
        string deepBefore = Snapshot();
        Reject(() => ScreenEditService.Reparent(deep, "branch", "depth63", 0));
        Reject(() => ScreenEditService.Reparent(deep, "branch", "depth62", 2));
        Reject(() => ScreenEditService.Reparent(deep, "depth0", "leaf", 0));
        Check(Snapshot() == deepBefore, "A rejected hierarchy move partially detached or reordered its subtree.");
        ScreenEditService.Reparent(deep, "branch", "depth62", 1);
        Check(ScreenEditService.Flatten(deep).Single(static row => row.element.id == "leaf").depth == 64,
            "An exact-budget hierarchy move was rejected or lost its descendant.");
        string Snapshot() => string.Join("|", ScreenEditService.Flatten(deep).Select(static row => row.element.id + ":" + row.parent?.id));
    }
    private static void Reject(Action operation) { try { operation(); } catch (InvalidOperationException) { return; } catch (ArgumentException) { return; } throw new InvalidOperationException("Invalid hierarchy move was accepted."); }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

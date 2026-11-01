using System;
using System.Linq;
using Inno.Editor.Interactions;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.UI.Binding;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class UiHierarchyDropContracts
{
    internal static void PlacementAndActualPaintOrder()
    {
        var screen = Screen();
        var move = UiHierarchyDropService.Describe(screen, "third", "first", EditorDropPlacement.Before);
        Check(move.parentId == "root" && move.index == 0 && !move.changesParent && !move.changesRowContext, "A same-parent drop was resolved before source removal or incorrectly changed context.");
        ScreenEditService.Reparent(screen, move.sourceId, move.parentId, move.index);
        Check(screen.root.children.Select(static item => item.id).SequenceEqual(new[] { "third", "first", "second", "group" }), "Before placement lost authored sibling order.");
        var after = UiHierarchyDropService.Describe(screen, "third", "second", EditorDropPlacement.After);
        ScreenEditService.Reparent(screen, after.sourceId, after.parentId, after.index);
        Check(screen.root.children.Select(static item => item.id).SequenceEqual(new[] { "first", "second", "third", "group" }), "After placement counted the removed source twice.");
        var into = UiHierarchyDropService.Describe(screen, "second", "group", EditorDropPlacement.Into);
        Check(into.changesParent && !into.changesRowContext, "A new layout/clip parent did not require review or invented a row context.");
        ScreenEditService.Reparent(screen, into.sourceId, into.parentId, into.index);
        Check(ScreenEditService.Find(screen, "group").children.Single().id == "second" && ScreenEditService.Find(screen, "second").layout.x == 12,
            "Tree movement guessed new coordinates or lost the subtree's identity.");
        var frame = new UiDocumentInstance(screen, static (_, _) => (20, 20)).BuildFrame(new Empty());
        Check(Array.FindIndex(frame.items.ToArray(), static item => item.id == "third") < Array.FindIndex(frame.items.ToArray(), static item => item.id == "second")
            && frame.items.Single(static item => item.id == "second").text == "Second", "The ordinary runtime did not use the new tree paint order and original content.");
    }
    internal static void DataContextsAndAtomicRejection()
    {
        var screen = Screen(); var group = ScreenEditService.Find(screen, "group"); group.repeatBinding = "firstRows";
        ScreenEditService.Reparent(screen, "second", "group", 0);
        var other = new UiElementDefinition { id = "other", kind = UiElementKind.Vertical, repeatBinding = "secondRows" };
        screen.root.children = [.. screen.root.children, other];
        Check(UiHierarchyDropService.Describe(screen, "second", "other", EditorDropPlacement.Into).changesRowContext,
            "Moving a row-bound control to another repeater failed to expose its changed item context.");
        Check(!UiHierarchyDropService.Describe(screen, "group", "third", EditorDropPlacement.Before).changesRowContext,
            "Moving a complete repeater incorrectly treated its owned child rows as inherited data.");
        string before = string.Join("|", ScreenEditService.Flatten(screen).Select(static row => row.element.id + ":" + row.parent?.id));
        Reject(() => UiHierarchyDropService.Describe(screen, "group", "second", EditorDropPlacement.Into));
        Reject(() => UiHierarchyDropService.Describe(screen, "root", "first", EditorDropPlacement.Into));
        Reject(() => UiHierarchyDropService.Describe(screen, "first", "root", EditorDropPlacement.Before));
        Reject(() => UiHierarchyDropService.Describe(screen, "first", "first", EditorDropPlacement.After));
        Reject(() => UiHierarchyDropService.Describe(screen, "first", "third", EditorDropPlacement.None));
        Reject(() => UiHierarchyDropService.Describe(screen, "missing", "group", EditorDropPlacement.Into));
        Check(before == string.Join("|", ScreenEditService.Flatten(screen).Select(static row => row.element.id + ":" + row.parent?.id)), "An invalid drop partially changed ownership.");
    }
    private static ScreenAsset Screen() => new() { width = 640, height = 480, root = new() { id = "root", children = [
        new() { id = "first", kind = UiElementKind.Text, text = "First" },
        new() { id = "second", kind = UiElementKind.Text, text = "Second", layout = new() { x = 12, width = 120, height = 40 } },
        new() { id = "third", kind = UiElementKind.Text, text = "Third" },
        new() { id = "group", kind = UiElementKind.Vertical, layout = new() { y = 100, width = 400, height = 200 } } ] } };
    private sealed class Empty : UiDataSource
    {
        public override bool TryRead(string path, out NovelValue value) { value = NovelValue.String(""); return false; }
        public override bool TryWrite(string path, NovelValue value) => false;
    }
    private static void Reject(Action operation) { try { operation(); } catch (InvalidOperationException) { return; } throw new InvalidOperationException("An invalid tree drop was accepted."); }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}

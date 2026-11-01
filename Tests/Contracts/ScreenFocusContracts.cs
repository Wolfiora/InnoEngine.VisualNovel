using System;
using System.Collections.Generic;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.UI.Binding;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class ScreenFocusContracts
{
    internal static void AuthoredFocus()
    {
        var screen = new ScreenAsset { width = 500, height = 300, initialFocus = "preferred", root = new() { id = "root", kind = UiElementKind.Vertical,
            layout = new() { width = 500, height = 300 }, children = [
                new() { id = "fallback", kind = UiElementKind.Button, layout = new() { width = 200, height = 50 } },
                new() { id = "preferred", kind = UiElementKind.Button, enabledBinding = "enabled", layout = new() { width = 200, height = 50 } }
            ] } };
        var values = new FocusData { enabled = true }; var view = new UiDocumentInstance(screen, static (_, _) => (20, 20)); view.BuildFrame(values);
        Check(view.focusedElement == "preferred", "Authored initial focus was ignored after layout.");
        values.enabled = false; view.BuildFrame(values); Check(view.focusedElement == "fallback", "An unavailable preferred target retained input focus.");
        values.enabled = true; view.RequestInitialFocus(); view.BuildFrame(values); Check(view.focusedElement == "preferred", "Explicit page reentry did not restore the preferred control.");
        var copy = UiDefinitionCopy.Copy(screen); ScreenEditService.Remove(copy, "preferred");
        Check(copy.initialFocus.Length == 0 && screen.initialFocus == "preferred", "Subtree deletion left a dangling focus reference or mutated the source.");
        screen.root.children[1].behindStage = true; bool rejected = false;
        try { new UiDocumentInstance(screen, static (_, _) => (20, 20)); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Decorative underlays were offered as keyboard focus targets.");
        screen.root.children[1].behindStage = false; screen.root.repeatBinding = "rows"; rejected = false;
        try { new UiDocumentInstance(screen, static (_, _) => (20, 20)); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "A repeated template identity was mistaken for a stable static control.");
    }
    private sealed class FocusData : UiDataSource
    {
        internal bool enabled;
        public override bool TryRead(string path, out NovelValue value) { value = NovelValue.Boolean(enabled); return path == "enabled"; }
        public override bool TryWrite(string path, NovelValue value) => false;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

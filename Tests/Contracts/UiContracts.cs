using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.UI.Binding;
using Inno.VisualNovel.UI.Documents;
using Inno.VisualNovel.UI.Input;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class UiContracts
{
    internal static void SelectorModalOwnership(Inno.Core.Serialization.SerializationRegistry native)
    {
        var data = new Data(); data.values["mode"] = NovelValue.Enum("a");
        var selector = new UiElementDefinition { id = "mode", kind = UiElementKind.Selector, binding = "mode", layout = new() { width = 600, height = 64 }, options = [
            new() { id = "a", text = "Alpha", value = NovelValue.Enum("a") }, new() { id = "b", text = "Disabled", value = NovelValue.Enum("b"), enabled = false }, new() { id = "c", text = "Charlie", value = NovelValue.Enum("c") }] };
        ScreenAsset page = Page(new() { id = "root", kind = UiElementKind.Vertical, layout = new() { width = 600, height = 300 }, children = [selector, Button("behind", "must-not-fire")] });
        page.root = native.Deserialize<UiElementDefinition>(native.Serialize(page.root));
        var document = new UiDocumentInstance(page, Measure);
        document.BuildFrame(data); document.HandleInput(new() { confirm = true }, data);
        Check(document.popupOpen && data.values["mode"].text == "a", "Opening a selector committed an option immediately.");
        document.HandleInput(new() { composing = true, next = true, confirm = true, cancel = true }, data);
        Check(document.popupOpen && data.values["mode"].text == "a", "IME composition navigated or closed a selector.");
        document.HandleInput(new() { next = true }, data); var result = document.HandleInput(new() { confirm = true }, data);
        Check(result.consumed && result.actions.Count == 0 && !document.popupOpen && data.values["mode"].Equals(NovelValue.Enum("c")), "Selector confirmation did not skip disabled options or leaked into underlying actions.");
        document.HandleInput(new() { confirm = true }, data);
        Check(document.HandleInput(new() { cancel = true }, data).cancelConsumed && !document.popupOpen && document.focusedElement == "mode", "Dropdown cancellation failed to retain the owning control's focus.");
        document.HandleInput(new() { confirm = true }, data);
        Check(document.HandleInput(new() { pointerInside = true, pressed = true, x = 500, y = 290 }, data).consumed && !document.popupOpen, "Outside dismissal leaked a pointer press.");
        selector = page.root.children[0]; selector.optionsBinding = "options"; data.values["mode"] = NovelValue.String("a");
        data.rows = [TypedRow("a"), TypedRow("b"), TypedRow("c")];
        document = new(page, Measure); document.BuildFrame(data); document.HandleInput(new() { confirm = true }, data);
        data.rows = [TypedRow("c"), TypedRow("b"), TypedRow("a")];
        document.HandleInput(new() { next = true }, data); document.HandleInput(new() { confirm = true }, data);
        Check(data.values["mode"].text == "c", "Reordered dynamic options changed selection identity into an array index.");

        static UiDataRow TypedRow(string id) => new(id, new Dictionary<string, NovelValue> { ["text"] = NovelValue.String(id), ["value"] = NovelValue.String(id) });
    }
    internal static void ScrollFocusAndSpace()
    {
        var data = new Data();
        ScreenAsset screen = Page(new() { id = "scroll", kind = UiElementKind.Scroll, layout = new() { width = 600, height = 160, gap = 10, clip = true },
            children = Enumerable.Range(0, 5).Select(index => Button("row" + index, "activate")).ToArray() });
        var document = new UiDocumentInstance(screen, Measure);
        document.BuildFrame(data);
        for (int index = 0; index < 4; index++) document.HandleInput(new() { next = true }, data);
        UiDrawItem last = document.BuildFrame(data).items.Single(item => item.id == "row4");
        Check(document.focusedElement == "row4" && last.bounds.y >= last.clip.y && last.bounds.y + last.bounds.height <= last.clip.y + last.clip.height,
            "Keyboard navigation could not reveal a control below the scroll viewport.");
        data.values["name"] = NovelValue.String("");
        var input = new UiDocumentInstance(Page(new() { id = "name", kind = UiElementKind.Input, binding = "name", action = "submit", layout = new() { width = 600, height = 80 } }), Measure);
        Check(input.HandleInput(new() { space = true, text = [" "] }, data).actions.Count == 0 && data.values["name"].text == " ", "Typing a space submitted a focused input control.");
        data.values["count"] = NovelValue.Integer(6);
        var slider = new UiDocumentInstance(Page(new() { id = "count", kind = UiElementKind.Slider, binding = "count", minimum = 0, maximum = 20, step = 2, layout = new() { width = 600, height = 64 } }), Measure);
        slider.HandleInput(new() { increment = true }, data);
        Check(data.values["count"].Equals(NovelValue.Integer(8)), "Keyboard slider input changed an Integer binding into Number or ignored its step.");
        slider.HandleInput(new() { composing = true, increment = true }, data);
        Check(data.values["count"].integer == 8, "IME composition changed a numeric setting.");
        slider.HandleInput(new() { pointerInside = true, pressed = true, x = 150, y = 20 }, data);
        Check(data.values["count"].Equals(NovelValue.Integer(6)), "Pointer slider input did not preserve integer type and quantization.");
    }
    internal static void LayoutAndOwnership()
    {
        ScreenAsset source = Page(new() { id = "stack", kind = UiElementKind.Vertical, layout = new() { width = 600, height = 300, gap = 10 }, children =
            [Button("first", "one"), new() { id = "fill", kind = UiElementKind.Panel, layout = new() { widthMode = UiSizeMode.Fill, heightMode = UiSizeMode.Fill } }] });
        var document = new UiDocumentInstance(source, Measure);
        source.root.children[0].layout.height = 999;
        UiFrame frame = document.BuildFrame(new Data());
        UiDrawItem first = frame.items.Single(item => item.id == "first"), fill = frame.items.Single(item => item.id == "fill");
        Check(first.bounds.height == 80 && fill.bounds.y == 90 && fill.bounds.height == 210, "Stack fill or source isolation failed.");
        Check(fill.bounds.width == 600, "Cross-axis fill failed.");
        document.SetFontScale(2);
        Check(document.BuildFrame(new Data()).items.Single(item => item.id == "first").fontSize == 56, "Text scaling did not use authored sizes.");
        document.SetFontScale(1);
        Check(document.BuildFrame(new Data()).items.Single(item => item.id == "first").fontSize == 28, "Text scaling accumulated drift.");
    }

    internal static void RepeatedRowsAndConsumption()
    {
        var data = new Data();
        var row = Button("row", "choose"); row.binding = "item.text"; row.argumentBinding = "item.id"; row.enabledBinding = "item.enabled";
        ScreenAsset screen = Page(new() { id = "list", kind = UiElementKind.Vertical, repeatBinding = "choices", layout = new() { width = 600, height = 300 }, children = [row] });
        var document = new UiDocumentInstance(screen, Measure);
        Check(document.BuildFrame(data).items.Count == 1, "Empty repeat displayed its unbound authoring template.");
        data.rows = [Row("a", "Alpha", true), Row("b", "Beta", false)];
        UiFrame frame = document.BuildFrame(data);
        Check(frame.items.Count == 3 && frame.items[1].text == "Alpha", "Repeat did not expand stable rows.");
        var pressed = document.HandleInput(new() { x = 20, y = 20, pointerInside = true, pressed = true }, data);
        Check(pressed.consumed && pressed.actions.Count == 0, "Pointer press leaked below a control.");
        var released = document.HandleInput(new() { x = 20, y = 20, pointerInside = true, released = true }, data);
        Check(released.consumed && released.actions.Single().argument == "a", "One release did not activate exactly its stable row.");
        data.rows = [Row("a", "Changed", true)];
        Check(document.BuildFrame(data).items[1].text == "Changed", "Stable row binding retained stale values.");
        data.rows = [Row("b", "Beta", false)];
        document.HandleInput(new() { x = 20, y = 20, pointerInside = true, pressed = true }, data);
        Check(document.HandleInput(new() { x = 20, y = 20, pointerInside = true, released = true }, data).actions.Count == 0, "Disabled row activated.");
    }

    internal static void FocusTextAndModal()
    {
        var data = new Data(); data.values["name"] = NovelValue.String("");
        var input = new UiElementDefinition { id = "name", kind = UiElementKind.Input, binding = "name", maxLength = 2, layout = new() { width = 600, height = 80 } };
        ScreenAsset page = Page(new() { id = "root", kind = UiElementKind.Vertical, layout = new() { width = 600, height = 300 }, children = [input, Button("done", "done")] });
        page.modal = true;
        var document = new UiDocumentInstance(page, Measure);
        document.BuildFrame(data);
        Check(document.focusedElement == "name", "Initial keyboard focus did not reach the first eligible control.");
        document.HandleInput(new() { text = ["A\u0301👨‍👩‍👧‍👦多"] }, data);
        Check(data.values["name"].text == "A\u0301👨‍👩‍👧‍👦", "Input limit split a grapheme cluster.");
        document.HandleInput(new() { backspace = true }, data);
        Check(data.values["name"].text == "A\u0301", "Backspace did not remove exactly one grapheme.");
        var composition = document.HandleInput(new() { composing = true, confirm = true, next = true }, data);
        Check(composition.consumed && composition.actions.Count == 0 && document.focusedElement == "name", "IME composition triggered navigation or confirmation.");
        document.HandleInput(new() { next = true }, data);
        Check(document.HandleInput(new() { confirm = true }, data).actions.Single().action == "done", "Keyboard confirmation failed.");
        Check(document.HandleInput(new(), data).consumed, "Modal page did not block lower input.");
    }

    private static ScreenAsset Page(UiElementDefinition root) => new() { width = 600, height = 300, root = root };
    private static UiElementDefinition Button(string id, string action) => new() { id = id, kind = UiElementKind.Button, action = action, layout = new() { width = 600, height = 80 } };
    private static (float, float) Measure(UiElementDefinition item, string text) => (text.Length * item.style.fontSize, item.style.fontSize);
    private static UiDataRow Row(string key, string text, bool enabled) => new(key, new Dictionary<string, NovelValue>
    { ["id"] = NovelValue.String(key), ["text"] = NovelValue.String(text), ["enabled"] = NovelValue.Boolean(enabled) });
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private sealed class Data : UiDataSource
    {
        internal readonly Dictionary<string, NovelValue> values = new(StringComparer.Ordinal);
        internal UiDataRow[] rows = [];
        public override bool TryRead(string path, out NovelValue value) => values.TryGetValue(path, out value!);
        public override bool TryWrite(string path, NovelValue value) { values[path] = value.Copy(); return true; }
        public override IReadOnlyList<UiDataRow> ReadRows(string path) => rows;
    }
}

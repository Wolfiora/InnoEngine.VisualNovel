using System;
using System.Globalization;
using System.Linq;
using Inno.Core.Serialization;
using Inno.Rendering.Assets;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Rendering.Frames;
using Inno.VisualNovel.UI.Binding;
using Inno.VisualNovel.UI.Documents;
using Inno.VisualNovel.UI.Input;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class TextInputContracts
{
    internal static void EditingGraphemesLimitsAndNativeCapture(SerializationRegistry native)
    {
        var data = new Data { value = "A👩‍💻e\u0301B", maximum = 8 }; var page = Page(100);
        var document = new UiDocumentInstance(page, Measure); document.BuildFrame(data);
        Edit(new() { textLeft = true }); Edit(new() { backspace = true });
        Check(data.value == "A👩‍💻B", "Backspace split a combining cluster or deleted at the end instead of the caret.");
        Edit(new() { delete = true }); Check(data.value == "A👩‍💻", "Delete did not remove the following grapheme.");
        Edit(new() { home = true, selecting = true });
        Check(document.BuildFrame(data).items.Single(static item => item.id == "field").inputPresentation?.selection is not null, "Shift+Home did not expose a transient selection.");
        Edit(new() { text = ["界"] }); Check(data.value == "界", "A commit did not replace the selected range.");
        Edit(new() { composing = true, delete = true, home = true, selectAll = true });
        Edit(new() { text = ["🇯🇵"] }); Check(data.value == "界🇯🇵", "Composition commands moved the caret or a flag sequence lost its grapheme boundary.");
        data.maximum = 2; Edit(new() { home = true }); Edit(new() { text = ["ABCD"] });
        Check(data.value == "界🇯🇵", "Insertion at the limit discarded the existing suffix.");
        Edit(new() { selectAll = true }); Edit(new() { text = ["e\u0301👨‍👩‍👧‍👦XYZ"] });
        Check(data.value == "e\u0301👨‍👩‍👧‍👦", "Bounded replacement cut a family emoji or removed a retained grapheme.");
        data.reject = true; Edit(new() { selectAll = true }); Edit(new() { text = ["Z"] });
        Check(data.value == "e\u0301👨‍👩‍👧‍👦" && document.BuildFrame(data).items.Single(static item => item.id == "field").inputPresentation?.selection is not null,
            "A rejected bound write changed the displayed value or lost its retry selection.");
        UiFrame capture = document.BuildFrame(data, false);
        Check(capture.items.All(static item => item.inputPresentation is null), "A noninteractive capture retained caret or selection state.");
        var restored = native.Deserialize<UiFrameSnapshot>(native.Serialize(UiFrameSnapshot.Capture(capture))).Restore(
            static _ => throw new InvalidOperationException("Unexpected font lookup."), static _ => throw new InvalidOperationException("Unexpected image lookup."));
        Check(restored.items.All(static item => item.inputPresentation is null) && data.value == "e\u0301👨‍👩‍👧‍👦", "Native paint restoration retained transient controls or truncated the underlying binding.");
        void Edit(UiInputSnapshot input) => Check(document.HandleInput(input, data).consumed, "Focused text editing leaked input to narrative.");
    }

    internal static void PointerScrollingWordSelectionAndRendering()
    {
        var data = new Data { value = "0123456789" }; var document = new UiDocumentInstance(Page(60), Measure);
        var end = document.BuildFrame(data).items.Single(static item => item.id == "field");
        Check(document.TryGetFocusedInputText(out string complete) && complete == data.value, "A native input bridge received the clipped paint window instead of the complete bound value.");
        Check(end.text == "56789" && end.inputPresentation?.caret is { } endCaret && endCaret.x >= end.bounds.x && endCaret.x + endCaret.width <= end.bounds.x + end.bounds.width,
            "A long input did not pan to its caret or painted the caret outside the control.");
        document.HandleInput(new() { textLeft = true }, data); var beforeCapture = document.BuildFrame(data).items.Single(static item => item.id == "field");
        document.BuildFrame(data, false); var afterCapture = document.BuildFrame(data).items.Single(static item => item.id == "field");
        Check(beforeCapture.text == afterCapture.text && beforeCapture.inputPresentation == afterCapture.inputPresentation, "Pure capture changed the active input viewport or selection geometry.");
        document.HandleInput(new() { home = true }, data);
        Check(document.BuildFrame(data).items.Single(static item => item.id == "field").text == "01234", "Home did not restore the leading viewport.");
        document.HandleInput(new() { x = 34.1f, y = 24, pointerInside = true, pressed = true }, data);
        document.HandleInput(new() { x = 54.1f, y = 24, pointerInside = true, held = true }, data);
        var released = document.HandleInput(new() { x = 54.1f, y = 24, pointerInside = true, released = true }, data);
        UiFrame selected = document.BuildFrame(data); var item = selected.items.Single(static item => item.id == "field");
        Check(released.consumed && released.actions.Count == 0 && item.inputPresentation?.selection is not null, "Text dragging activated another control or lost pointer capture.");
        var composed = new NovelFrameComposer().Compose(selected, new MaterialAsset());
        Check(composed.quads.Any(quad => quad.bounds.Equals(item.inputPresentation!.selection!.Value))
            && composed.quads.Any(quad => quad.bounds.Equals(item.inputPresentation!.caret!.Value)), "The production compositor omitted input selection or caret geometry.");
        document.HandleInput(new() { text = ["X"] }, data);
        Check(data.value == "01X456789", "Pointer range replacement used display indices instead of full-text grapheme indices.");
        data.value = "alpha beta"; document.BuildFrame(data);
        document.HandleInput(new() { home = true }, data); document.HandleInput(new() { textRight = true, wordNavigation = true }, data);
        document.HandleInput(new() { textRight = true, wordNavigation = true, selecting = true }, data);
        document.HandleInput(new() { text = ["界"] }, data); Check(data.value == "alpha 界", "Word selection did not preserve the preceding separator.");
        data.value = "ab"; var centeredPage = Page(100); centeredPage.root.children[0].style.alignment = UiTextAlignment.Center;
        var centered = new UiDocumentInstance(centeredPage, Measure); centered.BuildFrame(data);
        centered.HandleInput(new() { x = 51, y = 24, pointerInside = true, pressed = true }, data);
        centered.HandleInput(new() { x = 51, y = 24, pointerInside = true, released = true }, data);
        centered.HandleInput(new() { text = ["X"] }, data); Check(data.value == "Xab", "Centered text pointer placement ignored the actual alignment offset.");
    }
    private static ScreenAsset Page(float width) => new() { width = 300, height = 120, initialFocus = "field", root = new()
    { id = "root", layout = new() { width = 300, height = 120 }, children = [new() { id = "field", kind = UiElementKind.Input, binding = "value", maxLength = 32,
        layout = new() { x = 10, y = 10, width = width, height = 44 }, style = new() { textPadding = 4, fontSize = 20, lineSpacing = 1 } },
        new() { id = "other", kind = UiElementKind.Button, action = "other", layout = new() { x = 180, y = 10, width = 100, height = 44 } }] } };
    private static (float width, float height) Measure(
        UiElementDefinition element,
        string text
    ) => (StringInfo.ParseCombiningCharacters(text).Length * 10, 20);
    private sealed class Data : UiDataSource
    {
        internal string value = string.Empty;
        internal int maximum = 32;
        internal bool reject;
        public override bool TryRead(
            string path,
            out NovelValue result
        ) { result = NovelValue.String(value); return path == "value"; }
        public override bool TryWrite(
            string path,
            NovelValue candidate
        ) {
            if (reject || path != "value" || candidate.kind != NovelValueKind.String) return false;
            value = candidate.text; return true;
        }
        public override int GetTextLimit(
            string path,
            int authoredLimit
        ) => Math.Min(maximum, authoredLimit);
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}

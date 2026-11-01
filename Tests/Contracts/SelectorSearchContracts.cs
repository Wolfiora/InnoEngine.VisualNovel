using System;
using System.Collections.Generic;
using System.Linq;
using Inno.Core.Serialization;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.UI.Binding;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class SelectorSearchContracts
{
    internal static void FilteringOwnershipAndPersistence(SerializationRegistry native)
    {
        var source = new Source(); var document = Document();
        document.HandleInput(new() { confirm = true }, source);
        Check(document.BuildFrame(source).items.Any(static item => item.id == "select/search"), "A long option list omitted its search hint.");
        document.HandleInput(new() { text = ["ChArLiE"] }, source);
        UiFrame filtered = document.BuildFrame(source);
        Check(Options(filtered).Single().id == "select/option/charlie" && source.value.text == "a", "Filtering changed the binding or failed case-insensitive label matching.");
        Check(!document.BuildFrame(source, false).items.Any(static item => item.id.StartsWith("select/", StringComparison.Ordinal)), "Save capture included transient search or option overlays.");
        var snapshot = native.Deserialize<UiFrameSnapshot>(native.Serialize(UiFrameSnapshot.Capture(document.BuildFrame(source, false))));
        Check(snapshot.items.Length == 1 && snapshot.items[0].text == "Alpha ▾", "A native capture retained the temporary query or option rows.");
        document.HandleInput(new() { composing = true, text = ["ignored"], cancel = true, confirm = true }, source);
        Check(document.popupOpen && Options(document.BuildFrame(source)).Single().id == "select/option/charlie", "Composition changed the query or confirmed a value.");
        document.HandleInput(new() { text = [" impossible"], space = true, confirm = true }, source);
        Check(document.popupOpen && source.value.text == "a" && document.BuildFrame(source).items.Any(static item => item.id == "select/no-options"), "A no-match query closed the popup or leaked its typing sample into confirmation.");
        Check(document.HandleInput(new() { cancel = true }, source).cancelConsumed && document.popupOpen && Options(document.BuildFrame(source)).Count() > 1, "The first Escape did not clear the filter without leaving the popup.");
        document.HandleInput(new() { text = ["e\u0301👨‍👩‍👧‍👦"] }, source); document.HandleInput(new() { backspace = true }, source);
        Check(document.BuildFrame(source).items.Single(static item => item.id == "select/search").text.EndsWith("e\u0301", StringComparison.Ordinal), "Search deletion split a joined emoji or preceding combining grapheme.");
        document.HandleInput(new() { selectAll = true, text = ["charlie"] }, source);
        source.rows = source.rows.Reverse().ToArray(); source.reject = true;
        document.HandleInput(new() { confirm = true }, source);
        Check(document.popupOpen && document.bindingError.Length > 0 && source.value.text == "a", "A rejected filtered write closed the popup or changed its model.");
        source.reject = false; document.HandleInput(new() { confirm = true }, source);
        Check(!document.popupOpen && source.value.text == "c" && document.focusedElement == "select", "A reordered filtered option committed by index or lost focus.");
        document.HandleInput(new() { confirm = true }, source);
        Check(Options(document.BuildFrame(source)).Count() > 1, "A reopened selector retained the old filter.");
        document.HandleInput(new() { text = ["stable-disabled"] }, source); document.HandleInput(new() { confirm = true }, source);
        Check(document.popupOpen && source.value.text == "c", "Searching a disabled option made it available.");
        document.HandleInput(new() { cancel = true }, source); document.HandleInput(new() { cancel = true }, source);
        Check(!document.popupOpen, "Escape could not retire the cleared search popup.");
    }

    internal static void PageNavigationAndPointerRows()
    {
        var source = new Source(); var document = Document();
        document.HandleInput(new() { confirm = true }, source);
        document.HandleInput(new() { end = true }, source);
        UiDrawItem last = Options(document.BuildFrame(source)).Single(static item => item.focused);
        Check(last.id == "select/option/row-29", "End did not reveal the last enabled option.");
        document.HandleInput(new() { pageUp = true }, source);
        Check(Options(document.BuildFrame(source)).Any(static item => item.focused && item.id != "select/option/row-29"), "PageUp did not reveal a preceding option.");
        document.HandleInput(new() { home = true }, source);
        Check(Options(document.BuildFrame(source)).First().focused, "Home did not select the first available option.");
        document.HandleInput(new() { text = ["row-24"] }, source);
        UiFrame frame = document.BuildFrame(source); UiDrawItem header = frame.items.Single(static item => item.id == "select/search");
        document.HandleInput(new() { pressed = true, pointerInside = true, x = header.bounds.x + 2, y = header.bounds.y + 2 }, source);
        document.HandleInput(new() { released = true, pointerInside = true, x = header.bounds.x + 2, y = header.bounds.y + 2 }, source);
        Check(document.popupOpen && source.value.text == "a", "Clicking the search header selected the first option.");
        UiDrawItem row = Options(document.BuildFrame(source)).Single();
        document.HandleInput(new() { pressed = true, pointerInside = true, x = row.bounds.x + 2, y = row.bounds.y + 2 }, source);
        document.HandleInput(new() { released = true, pointerInside = true, x = row.bounds.x + 2, y = row.bounds.y + 2 }, source);
        Check(!document.popupOpen && source.value.text == "24", "Filtered pointer coordinates did not exclude the header or used an original row index.");
    }

    private static IEnumerable<UiDrawItem> Options(UiFrame frame) => frame.items.Where(static item => item.id.StartsWith("select/option/", StringComparison.Ordinal));
    private static UiDocumentInstance Document() => new(new ScreenAsset { width = 600, height = 400, root = new() { id = "select", kind = UiElementKind.Selector,
        binding = "mode", optionsBinding = "rows", layout = new() { width = 600, height = 64 } } }, static (item, text) => (text.Length * 14, 28));
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class Source : UiDataSource
    {
        internal NovelValue value = NovelValue.Enum("a");
        internal bool reject;
        internal UiDataRow[] rows = new[] { Row("alpha", "Alpha", "a"), Row("stable-disabled", "Disabled", "b", false), Row("charlie", "Charlie", "c") }
            .Concat(Enumerable.Range(3, 27).Select(index => Row("row-" + index, "Option " + index, index.ToString()))).ToArray();
        public override bool TryRead(
            string path,
            out NovelValue result
        ) { result = value.Copy(); return path == "mode"; }
        public override bool TryWrite(
            string path,
            NovelValue result
        ) { if (reject || path != "mode") return false; value = result.Copy(); return true; }
        public override IReadOnlyList<UiDataRow> ReadRows(string path) => rows;
        private static UiDataRow Row(
            string id,
            string text,
            string value,
            bool enabled = true
        ) => new(id, new Dictionary<string, NovelValue> { ["text"] = NovelValue.String(text), ["value"] = NovelValue.Enum(value), ["enabled"] = NovelValue.Boolean(enabled) });
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.UI.Binding;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class RowBindingContracts
{
    internal static void InteractiveOwners()
    {
        foreach (var kind in new[] { UiElementKind.Toggle, UiElementKind.Input, UiElementKind.Slider, UiElementKind.Selector })
        {
            NovelValue initial = kind == UiElementKind.Toggle ? NovelValue.Boolean(false) : kind == UiElementKind.Slider ? NovelValue.Number(0.2) : NovelValue.String("a");
            var data = new Rows(initial);
            var control = new UiElementDefinition { id = "control", kind = kind, binding = "item.value", layout = new() { width = 200, height = 50 },
                options = [new() { id = "a", text = "A", value = NovelValue.String("a") }, new() { id = "b", text = "B", value = NovelValue.String("b") }] };
            var screen = new ScreenAsset { width = 400, height = 400, root = new() { id = "root", kind = UiElementKind.Vertical, repeatBinding = "rows",
                layout = new() { width = 400, height = 400, gap = 20 }, children = [control] } };
            var ui = new UiDocumentInstance(screen, static (_, _) => (80, 30)); var frame = ui.BuildFrame(data);
            var target = frame.items.Last(item => item.kind == kind); float x = target.bounds.x + target.bounds.width * 0.75f, y = target.bounds.y + target.bounds.height / 2;
            ui.HandleInput(new() { pointerInside = true, x = x, y = y, pressed = true }, data);
            ui.HandleInput(new() { pointerInside = true, x = x, y = y, released = true }, data);
            if (kind == UiElementKind.Input) ui.HandleInput(new() { text = ["x"] }, data);
            if (kind == UiElementKind.Selector) { ui.BuildFrame(data); ui.HandleInput(new() { next = true }, data); ui.HandleInput(new() { confirm = true }, data); }
            Check(data.values["first"].Equals(initial) && !data.values["second"].Equals(initial), "A repeated " + kind + " wrote without its exact stable owner row.");
            data.reverse = true; frame = ui.BuildFrame(data);
            Check(frame.items.Where(item => item.kind == kind).First().id.Contains("second", StringComparison.Ordinal), "Reordering rows replaced stable ownership with display indices.");
        }
    }
    private sealed class Rows(NovelValue initial) : UiDataSource
    {
        internal readonly Dictionary<string, NovelValue> values = new() { ["first"] = initial.Copy(), ["second"] = initial.Copy() };
        internal bool reverse;
        public override IReadOnlyList<UiDataRow> ReadRows(string path) => (reverse ? new[] { "second", "first" } : new[] { "first", "second" })
            .Select(key => new UiDataRow(key, new Dictionary<string, NovelValue> { ["value"] = values[key].Copy() })).ToArray();
        public override bool TryRead(string path, out NovelValue value) { value = NovelValue.String(""); return false; }
        public override bool TryWrite(string path, NovelValue value) => false;
        public override bool TryWrite(
            string path,
            NovelValue value,
            UiDataRow? row
        ) {
            if (path != "item.value" || row is null || !values.ContainsKey(row.key)) return false;
            values[row.key] = value.Copy(); return true;
        }
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}

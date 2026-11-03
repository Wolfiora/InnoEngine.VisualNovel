using System;
using System.Collections.Generic;
using System.Linq;
using Inno.Core.Serialization;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.UI.Binding;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class UiLayoutContracts
{
    internal static void MarginsFlowAndHitTesting()
    {
        var toggle = Button("flag", 100, 50); toggle.kind = UiElementKind.Toggle; toggle.binding = "test.flag"; toggle.text = "启用";
        var toggleData = new Data(); var toggleDocument = new UiDocumentInstance(Page(UiElementKind.Panel, 200, 100, toggle), Measure);
        var off = toggleDocument.BuildFrame(toggleData).items.Single(item => item.id == "flag");
        toggleData.flag = true;
        var on = toggleDocument.BuildFrame(toggleData).items.Single(item => item.id == "flag");
        Check(off.text == "启用" && off.progress == 0 && on.text == "启用" && on.progress == 1, "Checkbox presentation parsed a localized label instead of preserving the bound Boolean state.");
        var a = Button("a", 100, 40); a.layout.marginLeft = 10; a.layout.marginTop = 5; a.layout.marginRight = 20; a.layout.marginBottom = 15; a.layout.horizontalAlignment = UiAlignment.Center;
        var b = Button("b", 200, 30); b.layout.marginTop = 10; b.layout.horizontalAlignment = UiAlignment.End;
        var page = Page(UiElementKind.Vertical, 500, 300, a, b); page.root.layout.padding = 10; page.root.layout.gap = 10; page.root.layout.flowAlignment = UiFlowAlignment.SpaceBetween;
        var data = new Data(); var document = new UiDocumentInstance(page, Measure); var frame = document.BuildFrame(data);
        var first = frame.items.Single(static item => item.id == "a"); var last = frame.items.Single(static item => item.id == "b");
        Near(first.bounds.x, 195); Near(first.bounds.y, 15); Near(last.bounds.x, 290); Near(last.bounds.y, 260);
        document.HandleInput(new() { pointerInside = true, x = 200, y = 20, pressed = true }, data);
        Check(document.HandleInput(new() { pointerInside = true, x = 200, y = 20, released = true }, data).actions.Single().action == "a", "Aligned geometry and pointer hit testing diverged.");
        document.HandleInput(new() { pointerInside = true, x = 190, y = 20, pressed = true }, data);
        Check(!document.HandleInput(new() { pointerInside = true, x = 190, y = 20, released = true }, data).actions.Any(), "An element's unpainted margin received its button action.");
        foreach (var (alignment, expected) in new[] { (UiFlowAlignment.Start, 10f), (UiFlowAlignment.Center, 95f), (UiFlowAlignment.End, 180f), (UiFlowAlignment.SpaceAround, 52.5f) })
        {
            page.root.layout.flowAlignment = alignment; var placed = new UiDocumentInstance(page, Measure).BuildFrame(data);
            Near(placed.items.Single(static item => item.id == "a").bounds.y, expected + 5);
        }
        var fixedText = Button("content", 500, 20); fixedText.text = "1234"; fixedText.layout.widthMode = UiSizeMode.Content; fixedText.layout.marginLeft = fixedText.layout.marginRight = 10;
        var fill = Button("fill", 999, 20); fill.layout.widthMode = UiSizeMode.Fill; fill.layout.marginLeft = 5; fill.layout.marginRight = 15;
        var row = Page(UiElementKind.Horizontal, 200, 80, fixedText, fill); row.root.layout.padding = 10; row.root.layout.gap = 10;
        var rowFrame = new UiDocumentInstance(row, Measure).BuildFrame(data);
        Near(rowFrame.items.Single(static item => item.id == "content").bounds.width, 40);
        Near(rowFrame.items.Single(static item => item.id == "fill").bounds.width, 90); Near(rowFrame.items.Single(static item => item.id == "fill").bounds.x, 85);
    }
    internal static void NativeGridScrollAndOwnership(SerializationRegistry native)
    {
        var a = Button("a", 40, 20); a.layout.marginLeft = 5; a.layout.marginRight = 15; a.layout.marginTop = 3; a.layout.marginBottom = 7;
        a.layout.horizontalAlignment = UiAlignment.End; a.layout.verticalAlignment = UiAlignment.Center;
        var b = Button("b", 40, 30); b.layout.marginTop = 5; b.layout.marginBottom = 5; b.layout.horizontalAlignment = UiAlignment.Stretch; b.layout.verticalAlignment = UiAlignment.Stretch;
        var c = Button("c", 40, 20); var grid = Page(UiElementKind.Grid, 200, 200, a, b, c); grid.root.layout.gap = 10;
        var captured = native.Deserialize<UiLayoutDefinition>(native.Serialize(a.layout)); var copy = UiDefinitionCopy.Copy(grid);
        var data = new Data(); var instance = new UiDocumentInstance(copy, Measure); a.layout.marginLeft = 99; a.layout.horizontalAlignment = UiAlignment.Start;
        var frame = instance.BuildFrame(data); var first = frame.items.Single(static item => item.id == "a"); var second = frame.items.Single(static item => item.id == "b");
        Near(first.bounds.x, 40); Near(first.bounds.y, 8); Near(second.bounds.width, 95); Near(second.bounds.height, 30); Near(frame.items.Single(static item => item.id == "c").bounds.y, 50);
        Check(captured.marginLeft == 5 && captured.marginRight == 15 && captured.horizontalAlignment == UiAlignment.End && copy.root.children[0].layout.marginLeft == 5,
            "Native layout, runtime declaration or author copy lost independent margin/alignment data.");
        var tall = Button("tall", 100, 90); tall.kind = UiElementKind.Panel; tall.layout.marginTop = 5; tall.layout.marginBottom = 15;
        var scroll = Page(UiElementKind.Scroll, 200, 100, tall); scroll.root.layout.padding = 10;
        var scroller = new UiDocumentInstance(scroll, Measure); Near(scroller.BuildFrame(data).items.Single(static item => item.id == "tall").bounds.y, 15);
        scroller.HandleInput(new() { pointerInside = true, x = 20, y = 20, scroll = -1000 }, data);
        Near(scroller.BuildFrame(data).items.Single(static item => item.id == "tall").bounds.y, -15);
        foreach (float invalid in new[] { -1f, float.NaN, float.PositiveInfinity, 100001f })
        {
            scroll.root.layout.marginLeft = invalid; bool rejected = false;
            try { _ = new UiDocumentInstance(scroll, Measure); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Invalid native margins escaped layout validation.");
        }
    }
    private static ScreenAsset Page(
        UiElementKind kind,
        float width,
        float height,
        params UiElementDefinition[] children
    ) => new() { width = width, height = height, root = new() { id = "root", kind = kind, layout = new() { width = width, height = height }, children = children } };
    private static UiElementDefinition Button(
        string id,
        float width,
        float height
    ) => new() { id = id, kind = UiElementKind.Button, action = id, layout = new() { width = width, height = height }, style = new() { textPadding = 0 } };
    private static (float width, float height) Measure(
        UiElementDefinition element,
        string text
    ) => (text.Length * 10, 20);
    private sealed class Data : UiDataSource
    {
        internal bool flag;
        /// <inheritdoc />
        public override bool TryRead(
            string path,
            out NovelValue value
        ) { value = NovelValue.Boolean(flag); return path == "test.flag"; }
        /// <inheritdoc />
        public override bool TryWrite(
            string path,
            NovelValue value
        ) => false;
        /// <inheritdoc />
        public override IReadOnlyList<UiDataRow> ReadRows(string path) => [];
    }
    private static void Near(
        float actual,
        float expected
    ) { if (Math.Abs(actual - expected) > .001f) throw new InvalidOperationException($"Expected {expected}, got {actual}."); }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}

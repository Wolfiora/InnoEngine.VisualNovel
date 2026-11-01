using System;
using System.Linq;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.UI.Binding;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class ScrollBarContracts
{
    internal static void DragTrackAndOcclusion()
    {
        var source = new Source(); var document = new UiDocumentInstance(Page(), Measure);
        UiFrame first = document.BuildFrame(source); UiDrawItem thumb = Item(first, "list/scrollbar/thumb"), track = Item(first, "list/scrollbar/track");
        Check(thumb.bounds.height >= 24 && thumb.bounds.height < track.bounds.height, "Overflow did not produce a bounded proportionate thumb.");
        document.SetHighContrast(true); UiFrame contrast = document.BuildFrame(source);
        Check(Item(contrast, "list/scrollbar/thumb").background != Item(contrast, "list/scrollbar/track").background, "High contrast erased the scrollbar thumb.");
        document.SetHighContrast(false);
        Check(document.BuildFrame(source, false).items.All(static item => !item.id.Contains("/scrollbar/", StringComparison.Ordinal)), "Noninteractive capture retained scroll affordances.");
        document.HandleInput(new() { pressed = true, pointerInside = true, x = thumb.bounds.x + 2, y = thumb.bounds.y + 2 }, source);
        document.HandleInput(new() { held = true, pointerInside = false, x = thumb.bounds.x, y = track.bounds.y + track.bounds.height + 100 }, source);
        UiFrame bottom = document.BuildFrame(source); UiDrawItem last = Item(bottom, "row-9");
        Check(Item(bottom, "list/scrollbar/thumb").bounds.y > thumb.bounds.y && last.clip.height >= last.bounds.height - .01f, "Thumb capture stopped outside the viewport or failed to reveal the last row.");
        Check(document.HandleInput(new() { released = true, confirm = true, pointerInside = true, x = last.bounds.x + 10, y = last.bounds.y + 10 }, source).actions.Count == 0,
            "Releasing a dragged scrollbar activated an underlying control or confirmation.");
        document.HandleInput(new() { pressed = true, pointerInside = true, x = last.bounds.x + 10, y = last.bounds.y + 10 }, source);
        Check(document.HandleInput(new() { released = true, pointerInside = true, x = last.bounds.x + 10, y = last.bounds.y + 10 }, source).actions.Single().argument == "9", "Scrollbar retirement left ordinary button input captured.");

        document = new(Page(), Measure); first = document.BuildFrame(source); track = Item(first, "list/scrollbar/track");
        document.HandleInput(new() { pressed = true, pointerInside = true, x = track.bounds.x + 2, y = track.bounds.y + track.bounds.height - 2 }, source);
        Check(Item(document.BuildFrame(source), "row-0").bounds.y < Item(first, "row-0").bounds.y, "Track press did not page the owner.");
        float paged = Item(document.BuildFrame(source), "row-0").bounds.y;
        document.HandleInput(new() { held = true, pointerInside = true, x = track.bounds.x + 2, y = track.bounds.y + track.bounds.height - 2 }, source);
        Check(Item(document.BuildFrame(source), "row-0").bounds.y == paged, "A held track press became an unintended thumb jump.");
        document.CancelInput(); float before = Item(document.BuildFrame(source), "row-0").bounds.y;
        document.HandleInput(new() { held = true, x = track.bounds.x, y = track.bounds.y + 500 }, source);
        Check(Item(document.BuildFrame(source), "row-0").bounds.y == before, "Cancelling page ownership retained a live scrollbar drag.");

        var covered = Page(); covered.root.children = [.. covered.root.children, new() { id = "overlay", kind = UiElementKind.Panel,
            layout = new() { x = 50, y = 40, width = 300, height = 200 }, blocksPointer = true, style = new() { background = 0xFFFFFFFF } }];
        document = new(covered, Measure); first = document.BuildFrame(source); track = Item(first, "list/scrollbar/track");
        Check(first.items.ToList().FindIndex(static item => item.id == "overlay") > first.items.ToList().FindIndex(static item => item.id == "list/scrollbar/thumb"), "Scrollbars painted over later authored overlays.");
        document.HandleInput(new() { pressed = true, pointerInside = true, x = track.bounds.x + 2, y = track.bounds.y + track.bounds.height - 2 }, source);
        Check(Item(document.BuildFrame(source), "row-0").bounds.y == Item(first, "row-0").bounds.y, "A covered scrollbar stole an overlay's pointer input.");
        var fitting = Page(); fitting.root.children[0].children = [fitting.root.children[0].children[0]];
        Check(new UiDocumentInstance(fitting, Measure).BuildFrame(source).items.All(static item => !item.id.Contains("/scrollbar/", StringComparison.Ordinal)), "A fitting scroll area displayed a meaningless scrollbar.");
    }
    private static ScreenAsset Page() => new() { width = 600, height = 400, root = new() { id = "root", kind = UiElementKind.Panel, blocksPointer = false,
        layout = new() { width = 600, height = 400 }, children = [new() { id = "list", kind = UiElementKind.Scroll, layout = new() { x = 50, y = 40, width = 300, height = 200, clip = true },
            children = Enumerable.Range(0, 10).Select(index => new UiElementDefinition { id = "row-" + index, kind = UiElementKind.Button, action = "choose", argument = index.ToString(),
                layout = new() { widthMode = UiSizeMode.Fill, height = 60 } }).ToArray() }] } };
    private static UiDrawItem Item(
        UiFrame frame,
        string id
    ) => frame.items.Single(item => item.id == id);
    private static (float width, float height) Measure(
        UiElementDefinition element,
        string text
    ) => (text.Length * 14, 28);
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class Source : UiDataSource
    {
        public override bool TryRead(
            string path,
            out NovelValue value
        ) { value = NovelValue.Boolean(false); return false; }
        public override bool TryWrite(
            string path,
            NovelValue value
        ) => false;
    }
}

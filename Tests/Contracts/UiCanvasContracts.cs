using System;
using System.Linq;
using Inno.Core.Mathematics;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.UI.Binding;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class UiCanvasContracts
{
    internal static void MeasuredTransformsAndNativeLayout(SerializationRegistry native)
    {
        UiResizeEdges[] handles = [UiResizeEdges.Left, UiResizeEdges.Right, UiResizeEdges.Top, UiResizeEdges.Bottom,
            UiResizeEdges.Left | UiResizeEdges.Top, UiResizeEdges.Right | UiResizeEdges.Top, UiResizeEdges.Left | UiResizeEdges.Bottom, UiResizeEdges.Right | UiResizeEdges.Bottom];
        foreach (var edges in handles)
        {
            var page = Page(); Rect before = Bounds(page, "child"); var start = UiCanvasEditService.Capture(page, "child", before);
            UiCanvasEditService.Transform(page, start, 12, -14, edges);
            Rect after = Bounds(page, "child");
            if (edges.HasFlag(UiResizeEdges.Left)) Near(after.x + after.width, before.x + before.width);
            if (edges.HasFlag(UiResizeEdges.Right)) Near(after.x, before.x);
            if (edges.HasFlag(UiResizeEdges.Top)) Near(after.y + after.height, before.y + before.height);
            if (edges.HasFlag(UiResizeEdges.Bottom)) Near(after.y, before.y);
            if ((edges & (UiResizeEdges.Left | UiResizeEdges.Right)) == 0) Near(after.width, before.width);
            if ((edges & (UiResizeEdges.Top | UiResizeEdges.Bottom)) == 0) Near(after.height, before.height);
            var layout = native.Deserialize<UiLayoutDefinition>(native.Serialize(page.root.children[0].layout));
            Check(layout.anchorX == .5f && layout.anchorY == .5f && layout.pivotX == .25f && layout.pivotY == .75f
                && layout.widthMode == UiSizeMode.Fixed && layout.heightMode == UiSizeMode.Fixed, "Canvas resizing destroyed authored anchoring, pivots or native sizing policy.");
            var direct = Page(); UiCanvasEditService.Transform(direct, UiCanvasEditService.Capture(direct, "child", before), 25, 30, edges);
            UiCanvasEditService.Transform(page, start, 25, 30, edges);
            Check(native.Serialize(page.root).SequenceEqual(native.Serialize(direct.root)), "Repeated pointer samples accumulated rounding or used the preceding sample as their origin.");
        }
        var snapped = Page(); var initial = Bounds(snapped, "child"); var move = UiCanvasEditService.Capture(snapped, "child", initial);
        UiCanvasEditService.Transform(snapped, move, 13, -17, grid: 8); var moved = Bounds(snapped, "child");
        Near(moved.x / 8, MathF.Round(moved.x / 8)); Near(moved.y / 8, MathF.Round(moved.y / 8)); Near(moved.width, initial.width); Near(moved.height, initial.height);
        var limit = Page(); initial = Bounds(limit, "child"); move = UiCanvasEditService.Capture(limit, "child", initial);
        UiCanvasEditService.Transform(limit, move, -10000, -10000, UiResizeEdges.Left | UiResizeEdges.Top);
        var clamped = Bounds(limit, "child"); Near(clamped.width, 200); Near(clamped.height, 160); Near(clamped.x + clamped.width, initial.x + initial.width); Near(clamped.y + clamped.height, initial.y + initial.height);
        foreach (var alignment in Enum.GetValues<UiCanvasAlignment>())
        {
            var page = Page(); initial = Bounds(page, "child"); var origin = UiCanvasEditService.Capture(page, "child", initial);
            Rect reference = new(40, 60, 600, 400); UiCanvasEditService.Align(page, origin, reference, alignment); var aligned = Bounds(page, "child");
            switch (alignment)
            {
                case UiCanvasAlignment.Left: Near(aligned.x, reference.x); Near(aligned.y, initial.y); break;
                case UiCanvasAlignment.HorizontalCenter: Near(aligned.x + aligned.width / 2, reference.x + reference.width / 2); Near(aligned.y, initial.y); break;
                case UiCanvasAlignment.Right: Near(aligned.x + aligned.width, reference.x + reference.width); Near(aligned.y, initial.y); break;
                case UiCanvasAlignment.Top: Near(aligned.y, reference.y); Near(aligned.x, initial.x); break;
                case UiCanvasAlignment.VerticalCenter: Near(aligned.y + aligned.height / 2, reference.y + reference.height / 2); Near(aligned.x, initial.x); break;
                case UiCanvasAlignment.Bottom: Near(aligned.y + aligned.height, reference.y + reference.height); Near(aligned.x, initial.x); break;
            }
            Near(aligned.width, initial.width); Near(aligned.height, initial.height);
        }
    }
    internal static void AutomaticOwnershipAndAtomicRejection(SerializationRegistry native)
    {
        var page = Page(); page.root.children[0].layout.widthMode = UiSizeMode.Fill;
        Rect before = Bounds(page, "child"); var start = UiCanvasEditService.Capture(page, "child", before);
        UiCanvasEditService.Transform(page, start, 7, 9); var moved = Bounds(page, "child"); Near(moved.x, before.x + 7); Near(moved.y, before.y + 9);
        byte[] unchanged = native.Serialize(page.root);
        Reject(() => UiCanvasEditService.Transform(page, start, 30, 0, UiResizeEdges.Right));
        Check(native.Serialize(page.root).SequenceEqual(unchanged), "Resizing a Fill axis silently changed its sizing mode or partially wrote layout.");
        Reject(() => UiCanvasEditService.Transform(page, start, 0, 0, UiResizeEdges.Left | UiResizeEdges.Right));
        Reject(() => UiCanvasEditService.Transform(page, start, float.NaN, 0));
        Reject(() => UiCanvasEditService.Transform(page, start, 0, 0, grid: -1));
        page.root.children[0].layout.anchorX = .25f; unchanged = native.Serialize(page.root);
        Reject(() => UiCanvasEditService.Transform(page, start, 3, 5));
        Check(native.Serialize(page.root).SequenceEqual(unchanged), "An obsolete canvas gesture overwrote changed anchoring.");
        Reject(() => UiCanvasEditService.Capture(page, "root", before));
        page.root.kind = UiElementKind.Horizontal; Reject(() => UiCanvasEditService.Capture(page, "child", before));
        page.root.kind = UiElementKind.Panel; page.root.repeatBinding = "rows";
        Reject(() => UiCanvasEditService.Capture(page, "child", before));
        page = Page(); start = UiCanvasEditService.Capture(page, "child", Bounds(page, "child")); page.root.repeatBinding = "rows"; unchanged = native.Serialize(page.root);
        Reject(() => UiCanvasEditService.Transform(page, start, 8, 8)); Check(native.Serialize(page.root).SequenceEqual(unchanged), "A changed repeat owner accepted an obsolete absolute gesture.");
        page = Page(); Reject(() => UiCanvasEditService.Capture(page, "child", new(0, 0, 0, 10)));
        start = UiCanvasEditService.Capture(page, "child", Bounds(page, "child")); unchanged = native.Serialize(page.root);
        Reject(() => UiCanvasEditService.Align(page, start, new(0, 0, -1, 10), UiCanvasAlignment.Left));
        Check(native.Serialize(page.root).SequenceEqual(unchanged), "Rejected alignment changed the authored layout.");
        var restored = new ScreenAsset { width = 800, height = 600, root = native.Deserialize<UiElementDefinition>(native.Serialize(page.root)) };
        var restoredStart = UiCanvasEditService.Capture(restored, "child", Bounds(restored, "child"));
        UiCanvasEditService.Transform(restored, restoredStart, 1, 1);
        Near(Bounds(restored, "child").x, Bounds(page, "child").x + 1);
    }
    internal static Rect Bounds(
        ScreenAsset page,
        string id
    ) => new UiDocumentInstance(page, static (_, _) => (30, 20)).BuildFrame(new Data(), false).items.Single(item => item.id == id).bounds;
    private static ScreenAsset Page() => new() { width = 800, height = 600,
        root = new() { id = "root", kind = UiElementKind.Panel, layout = new() { width = 800, height = 600, padding = 20 }, children = [new()
        { id = "child", kind = UiElementKind.Panel, layout = new() { x = 13, y = 17, width = 120, height = 80, minWidth = 50, minHeight = 40, maxWidth = 200, maxHeight = 160,
            anchorX = .5f, anchorY = .5f, pivotX = .25f, pivotY = .75f } }] } };
    private sealed class Data : UiDataSource
    {
        public override bool TryRead(string path, out NovelValue value) { value = NovelValue.Boolean(false); return false; }
        public override bool TryWrite(string path, NovelValue value) => false;
    }
    private static void Near(double actual, double expected) => Check(Math.Abs(actual - expected) < .001, $"Measured canvas geometry expected {expected}, got {actual}.");
    private static void Reject(Action action)
    { try { action(); } catch (ArgumentException) { return; } catch (InvalidOperationException) { return; } throw new InvalidOperationException("An invalid canvas gesture was accepted."); }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

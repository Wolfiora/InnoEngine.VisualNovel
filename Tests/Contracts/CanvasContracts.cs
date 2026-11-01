using Inno.Core.Mathematics;
using System;
using Vector2 = System.Numerics.Vector2;
using Inno.Rendering.Assets;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Rendering.Frames;
using Inno.VisualNovel.UI.Documents;
using Inno.VisualNovel.UI.Input;
using Inno.VisualNovel.UI.Layout;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class CanvasContracts
{
    internal static void NestedInputAndComposition()
    {
        foreach (var viewport in new[] { new Rect(0, 0, 1280, 720), new(0, 0, 1537, 961), new(0, 0, 3440, 1440), new(0, 0, 1001, 751) })
        {
            UiCanvasFit output = UiCanvasFit.Create(1280, 800, viewport, true);
            UiCanvasFit page = UiCanvasFit.Create(1920, 1080, new(0, 0, 1280, 800));
            Vector2 original = new(455.25f, 830.5f);
            Vector2 physical = output.Project(page.Project(original));
            Vector2 logical = page.Unproject(output.Unproject(physical));
            Check(Vector2.Distance(original, logical) < 0.001, "Nested canvas fitting lost pointer alignment at a fractional viewport size.");
            var sample = new UiInputSnapshot { x = page.Project(original).X, y = page.Project(original).Y, pointerInside = true, pressed = true, composing = true }.InCanvas(page);
            Check(sample.pressed && sample.composing && sample.pointerInside && Math.Abs(sample.x - original.X) < 0.001, "Input mapping discarded transitions or composition state.");
        }
        UiCanvasFit letterbox = UiCanvasFit.Create(1920, 1080, new(0, 0, 1280, 800));
        Check(!new UiInputSnapshot { x = 5, y = 5, pointerInside = true, pressed = true }.InCanvas(letterbox).pointerInside, "Letterbox clicks entered the underlying page.");
        var frame = new UiFrame(100, 100, [new("panel", UiElementKind.Panel, new(25, 20, 50, 30), new(0, 0, 100, 100), 0xFFFFFFFF, 0, "", null, 20, 0, null, false, 0)]);
        var composer = new NovelFrameComposer();
        NovelRenderFrame rendered = composer.Compose(frame, new MaterialAsset(), targetWidth: 200, targetHeight: 100);
        Check(rendered.width == 200 && rendered.height == 100 && rendered.quads.Count == 1 && rendered.quads[0].bounds.x == 75 && rendered.quads[0].clip.x == 50,
            "Composition did not fit authored UI and its clip into the final game canvas.");
        composer.Clear();
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

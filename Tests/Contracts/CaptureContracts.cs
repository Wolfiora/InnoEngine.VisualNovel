using System;
using System.IO;
using System.Linq;
using System.Threading;
using Inno.Rendering;
using Inno.Rendering.Assets;
using Inno.VisualNovel.Rendering.Capture;
using Inno.VisualNovel.Rendering.Frames;
using Inno.VisualNovel.UI.Imaging;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class CaptureContracts
{
    internal static void ImageFormats()
    {
        byte[] source = [0, 20, 200, 255, 200, 100, 50, 128, 10, 30, 60, 255, 80, 100, 120, 0];
        var image = new UiBitmap(2, 2, source); source[0] = 255;
        byte[] png = UiPngImage.Encode(image); var restored = UiPngImage.Decode(png);
        Check(png.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) && restored.width == 2 && restored.height == 2,
            "Thumbnail is not a standard complete PNG.");
        Check(image.pixels.Span[0] == 0 && image.pixels.ToArray().Zip(restored.pixels.ToArray()).All(static pair => Math.Abs(pair.First - pair.Second) <= 1), "PNG roundtrip lost alpha, orientation or bounded color precision.");
        png[20] ^= 1; bool rejected = false; try { UiPngImage.Decode(png); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "Damaged PNG bytes were accepted without chunk validation.");
        byte[] padded = [10, 20, 30, 255, 99, 99, 99, 99, 32, 16, 8, 128, 99, 99, 99, 99];
        var readback = new RenderTextureReadbackResult(new(1, 2, RenderTextureFormat.RGBA8, RenderTextureUsage.Readback), 0, 8, padded);
        var normalized = NovelReadbackImage.Normalize(readback, true);
        Check(normalized.pixels.Span[0] == 64 && normalized.pixels.Span[1] == 32 && normalized.pixels.Span[3] == 128 && normalized.pixels.Span[4] == 10,
            "Readback normalization ignored padding, bottom-left origin or premultiplied alpha.");
        rejected = false; try { NovelReadbackImage.Normalize(new(new(2, 2, RenderTextureFormat.RGBA8, RenderTextureUsage.Readback), 0, 3, [0, 0, 0]), false); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "Truncated readback reached image encoding.");
        Check(new UiBitmap(1, 2, new byte[8]).contentKey != new UiBitmap(2, 1, new byte[8]).contentKey, "Raster cache identity omitted image dimensions.");
        var paint = new Inno.VisualNovel.UI.Documents.UiFrame(100, 100, [new("generated", Inno.VisualNovel.Model.Interface.UiElementKind.Image, new(0, 0, 100, 100), new(0, 0, 100, 100), 0, 0xFFFFFFFF, "", null, 24, 0, null, false, 0,
            textResources: new([new() { key = "unused" }], []), bitmap: image)]);
        var frozen = Inno.VisualNovel.UI.Documents.UiFrameSnapshot.Capture(paint).Restore(_ => throw new Exception("Unused font was resolved."), _ => throw new Exception("No imported image exists."));
        Check(frozen.items[0].bitmap is { width: 2, height: 2 } && new NovelFrameComposer().Compose(frozen, new MaterialAsset()).quads.Single().texture.bitmap is not null,
            "Generated image did not survive neutral UI capture and production paint extraction.");
    }
    internal static void QueueOwnership()
    {
        var quads = new System.Collections.Generic.List<NovelDrawQuad> { new(new(0, 0, 100, 100), new(0, 0, 100, 100), 0xFFFFFFFF, new("white")) };
        var frame = new NovelRenderFrame(1920, 1080, new MaterialAsset(), quads); using var service = new NovelScreenshotService();
        var pending = Enumerable.Range(0, 4).Select(_ => service.Request(frame)).ToArray(); var transferred = service.Drain();
        quads.Clear();
        Check(transferred.Length == 4 && transferred.All(static item => item.frame.quads.Count == 1), "Capture retained mutable live-frame collection aliases.");
        bool rejected = false; try { service.Request(frame); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Transferred captures escaped the outstanding-request budget.");
        service.Dispose();
        Check(pending.All(static task => task.IsCompleted), "Retiring the scene left transferred screenshot tasks running.");
        foreach (var request in transferred) request.Dispose();
        using var cancel = new CancellationTokenSource(); using var owner = new NovelScreenshotService();
        var cancelled = owner.Request(frame, cancellationToken: cancel.Token); cancel.Cancel();
        Check(cancelled.IsCompleted && owner.Drain().Length == 0, "Cancelled queued frames were still submitted to rendering.");
    }
    internal static void TransferGraph()
    {
        var targets = NovelCaptureTargets.Create(320, 180);
        Check((targets.readback.usage & (RenderTextureUsage.ColorAttachment | RenderTextureUsage.DepthStencilAttachment | RenderTextureUsage.Storage)) == 0
            && targets.readback.usage.HasFlag(RenderTextureUsage.Readback) && targets.readback.usage.HasFlag(RenderTextureUsage.CopyDestination)
            && targets.image.usage.HasFlag(RenderTextureUsage.CopySource), "Readback was incorrectly combined with a raster attachment.");
        var capabilities = Inno.Build.Toolchains.Bgfx.Tools.BgfxTargetCapabilities.Create(GraphicsApi.Direct3D11);
        var graph = new RenderGraphBuilder(1, capabilities);
        var image = graph.CreateTexture("image", targets.image); var destination = graph.CreateTexture("readback", targets.readback);
        graph.AddRasterPass("clear image", new("vn.test.capture"), 0, static (_, _) => { }).UseColorAttachment(image, 0, RenderLoadAction.Clear);
        var copy = graph.AddCopyPass("transfer", new("vn.test.capture"), (image, destination), static (value, owner) => owner.commands.CopyTexture(value.image, value.destination));
        copy.CopyTexture(image, destination); graph.MarkOutput(destination);
        Check(graph.Validate().isValid, "Neutral capture draw/copy resource declarations failed real engine graph validation.");
        Check(graph.Compile().succeeded, "Neutral capture graph could not compile without a graphics device.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

using Inno.Core.Mathematics;
using System;
using System.Buffers.Binary;
using System.Linq;
using Inno.Core.Serialization;
using Inno.Rendering.Assets;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Rendering.Frames;
using Inno.VisualNovel.Rendering.Geometry;
using Inno.VisualNovel.UI.Binding;
using Inno.VisualNovel.UI.Documents;
using Inno.VisualNovel.UI.Imaging;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class NineSliceContracts
{
    internal static void GeometryAndNative(SerializationRegistry native)
    {
        var source = new NovelTextureSource("source"); var borders = new UiImageSlice { left = 2, right = 3, top = 1, bottom = 2, scale = 2 };
        var quads = NineSliceBuilder.Build(source, 10, 8, new(10, 20, 100, 60), new(0, 0, 200, 200), 0xAABBCC80, borders);
        Check(quads.Count == 9 && quads[0].bounds.width == 4 && quads[0].bounds.height == 2 && quads[2].bounds.width == 6,
            "Nine-slice corners stretched with the destination instead of preserving source-border scale.");
        Check(quads[4].uv == new Rect(.2f, .125f, .5f, .625f) && Math.Abs(quads.Sum(static item => item.bounds.width * item.bounds.height) - 6000) < .01f,
            "Nine-slice source regions or destination coverage contain gaps or overlap.");
        byte[] packed = QuadBatchBuilder.Pack(quads[4], 200, 200);
        Check(BinaryPrimitives.ReadSingleLittleEndian(packed.AsSpan(8)) == .2f && BinaryPrimitives.ReadSingleLittleEndian(packed.AsSpan(12)) == .125f,
            "The production vertex stream discarded sliced source UVs.");
        var tiny = NineSliceBuilder.Build(source, 10, 8, new(0, 0, 3, 2), new(0, 0, 3, 2), 0xFFFFFFFF, borders);
        Check(tiny.All(static item => item.bounds.width > 0 && item.bounds.height > 0) && Math.Abs(tiny.Sum(static item => item.bounds.width * item.bounds.height) - 6) < .001,
            "A small destination inverted, overlapped or lost its compressed corners.");
        borders.drawCenter = false;
        Check(NineSliceBuilder.Build(source, 10, 8, new(0, 0, 100, 60), new(0, 0, 100, 60), 0xFFFFFFFF, borders).Count == 8, "Border-only painting still filled the center.");
        borders.right = 100; bool rejected = false;
        try { NineSliceBuilder.Build(source, 10, 8, new(0, 0, 100, 60), new(0, 0, 100, 60), 0xFFFFFFFF, borders); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Source borders outside the imported image reached GPU geometry.");

        var screen = new ScreenAsset { width = 100, height = 60, root = new() { id = "root", kind = UiElementKind.Image, imageFit = UiImageFit.NineSlice,
            imageBinding = "generated", imageSlice = new() { left = 2, right = 2, top = 2, bottom = 2 }, layout = new() { width = 100, height = 60 },
            style = new() { background = 0, imageTint = 0x80C0FFFF, opacity = .5f } } };
        UiElementDefinition declaration = native.Deserialize<UiElementDefinition>(native.Serialize(screen.root));
        Check(declaration.imageSlice.left == 2 && declaration.style.imageTint == 0x80C0FFFF, "Native source serialization lost border or image-color authoring data.");
        var view = new UiDocumentInstance(screen, static (_, _) => (0, 0)); var frame = view.BuildFrame(new BitmapSource());
        Check(frame.items[0].imageTint == 0x80C0FF80, "Complete element opacity did not affect its image tint.");
        var saved = native.Deserialize<UiFrameSnapshot>(native.Serialize(UiFrameSnapshot.Capture(frame)));
        var restored = saved.Restore(_ => throw new Exception(), _ => throw new Exception());
        var draw = new NovelFrameComposer().Compose(restored, new MaterialAsset());
        Check(draw.quads.Count == 9 && draw.quads.All(static item => item.color == 0x80C0FF80 && item.texture.bitmap is not null), "Captured UI restoration bypassed nine-slice painting, tint or generated images.");
        screen.root.imageSlice.left = 8;
        Check(view.BuildFrame(new BitmapSource()).items[0].imageSlice!.left == 2, "Runtime border declarations retained an editable source alias.");
    }
    private sealed class BitmapSource : UiDataSource
    {
        private readonly UiBitmap m_bitmap = new(8, 8, Enumerable.Repeat((byte)255, 8 * 8 * 4).ToArray());
        public override UiBitmap? ReadBitmap(string path, UiDataRow? row) => path == "generated" ? m_bitmap : null;
        public override bool TryRead(string path, out Inno.VisualNovel.Model.Values.NovelValue value) { value = Inno.VisualNovel.Model.Values.NovelValue.Boolean(false); return false; }
        public override bool TryWrite(string path, Inno.VisualNovel.Model.Values.NovelValue value) => false;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

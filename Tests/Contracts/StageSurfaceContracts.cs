using System;
using System.Collections.Generic;
using System.Linq;
using System.Buffers.Binary;
using Inno.Core.Serialization;
using Inno.Rendering.Assets;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Rendering.Frames;
using Inno.VisualNovel.Rendering.Geometry;
using Inno.VisualNovel.Stage.Composition;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class StageSurfaceContracts
{
    internal static void CropPivotAndRestore(SerializationRegistry native)
    {
        var source = new StageAsset { layers = [new() { id = "crop", width = 100, height = 80,
            transform = new() { x = 100, y = 200, pivotX = 10, pivotY = 20, scaleX = -2, scaleY = 3 },
            surface = new() { sourceX = .5, sourceWidth = .5, cropX = .25, cropY = .25, cropWidth = .5, cropHeight = .5 } }] };
        var stage = new StageController([]); stage.SetStage(source);
        var first = stage.BuildFrame(1920, 1080).items.Single();
        Check(first.topLeft.X == 70 && first.topLeft.Y == 200 && first.bottomRight.X == -30 && first.bottomRight.Y == 320,
            "Local crop or reflected pixel pivot changed placement geometry.");
        Check(first.uv is { x: .625f, y: .25f, width: .25f, height: .5f }, "Source selection did not compose with local cropping.");
        source.layers[0].surface.cropX = 0; source.layers[0].transform.pivotX = 0;
        Check(stage.BuildFrame(1920, 1080).items.Single().topLeft == first.topLeft, "Active surface retained mutable author declarations.");
        StageState saved = native.Deserialize<StageState>(native.Serialize(stage.Capture()));
        stage.SetStage(new() { layers = [] }); stage.Restore(saved);
        Check(stage.BuildFrame(1920, 1080).items.Single().uv == first.uv, "Native snapshot lost normalized sampling.");
        saved.layers[0].surface.cropWidth = .9;
        bool rejected = false; try { stage.Restore(saved); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && stage.BuildFrame(1920, 1080).items.Single().topLeft == first.topLeft, "Rejected crop replaced the live stage.");
        saved = stage.Capture(); saved.layers[0].surface.cropWidth = 0; stage.Restore(saved);
        Check(!stage.hasContent && stage.BuildFrame(1920, 1080).items.Count == 0, "Empty crop left visible geometry.");
        var frozen = StageAssetCopy.Copy(source); frozen.layers[0].surface.cropY = 0;
        Check(source.layers[0].surface.cropY == .25, "Stage copy aliased surface values.");
    }

    internal static void ImagePlanAndVertices()
    {
        var image = new TextureAsset(1, 1, TextureColorSpace.Srgb, "png"); var mask = new TextureAsset(1, 1, TextureColorSpace.Srgb, "png"); var material = new MaterialAsset();
        var region = new Inno.Core.Mathematics.Rect(.25f, .5f, .5f, .25f);
        var plain = new StageDrawItem("plain", new(0, 0), new(100, 0), new(100, 100), new(0, 100), image, 0xFFFFFFFF, uv: region);
        var add = plain with { id = "add", blend = StageBlendMode.Add };
        var multiply = plain with { id = "multiply", blend = StageBlendMode.Multiply, mask = mask, maskMode = StageMaskMode.InverseLuminance,
            maskUv = new(.1f, .2f, .3f, .4f) };
        var page = new UiFrame(1920, 1080, []); var composer = new NovelFrameComposer();
        bool rejected = false;
        try { composer.Compose(page, material, new([plain, add, multiply])); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Stage blends silently rendered without the image material.");
        var frame = composer.Compose(page, material, new([plain, add, multiply]), effectsMaterial: material);
        var operations = Walk(frame.image!).OfType<NovelProcessImage>().ToArray();
        Check(operations.Any(item => item.config.X == 200) && operations.Any(item => item.config.X == 201)
            && operations.Any(item => item.config.X == 213), "Add, multiply or inverse luminance was omitted from the pure image plan.");
        var multiplyPlan = operations.Single(item => item.config.X == 201);
        Check(multiplyPlan.source is NovelProcessImage { config.X: 213 } && Walk(multiplyPlan.previous).OfType<NovelProcessImage>().Any(item => item.config.X == 200),
            "Mask was applied after blending or the existing backdrop was discarded.");
        var maskQuad = frame.image!.Paints().Single(quad => ReferenceEquals(quad.texture.image, mask));
        Check(maskQuad.uv == multiply.maskUv && maskQuad.color == 0xFFFFFFFF, "Mask inherited source tint or atlas sampling.");
        var flat = composer.Compose(page, material, new([plain]));
        Check(flat.image is null && flat.quads.Single().uv == region, "Ordinary crop unnecessarily required post-processing.");
        var bytes = QuadBatchBuilder.Pack(flat.quads.Single(), 1920, 1080);
        Check(BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(8, 4)) == .25f && BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(12, 4)) == .5f
            && BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(40, 4)) == .75f, "Packed vertices discarded normalized source coordinates.");
        var copy = NovelRenderFrameCopy.Copy(frame);
        Check(copy.image is not null && !ReferenceEquals(copy.image, frame.image) && copy.image.Paints().Any(quad => ReferenceEquals(quad.texture.image, mask)),
            "Delayed exact capture lost the borrowed mask dependency.");
    }

    internal static void NativeCharacter(
        CharacterAsset character,
        TextureAsset image,
        SerializationRegistry native
    ) {
        var source = CharacterAssetCopy.Copy(character); var part = source.poses[0].parts[0];
        part.mask = 0; part.surface = new() { cropWidth = .5, sourceWidth = .5, blend = StageBlendMode.Multiply, maskMode = StageMaskMode.Luminance };
        part.transform.pivotX = 10;
        var controller = new StageController(source.textures); controller.ShowCharacter("masked", source, "", "neutral", new() { x = 100, pivotX = 20 });
        var draw = controller.BuildFrame(1920, 1080).items[0];
        Check(draw.topLeft.X == 70 && draw.topRight.X == 120 && draw.uv!.Value.width == .25f
            && ReferenceEquals(draw.mask, image) && draw.blend == StageBlendMode.Multiply, "Character parts lost nested pivots, source, crop or mask.");
        var saved = native.Deserialize<StageState>(native.Serialize(controller.Capture())); controller.Restore(saved);
        Check(controller.BuildFrame(1920, 1080).items[0].mask?.identity.persistentId == image.identity.persistentId, "Native save did not resolve the canonical mask.");
        saved.layers[0].maskTextureId = Guid.NewGuid();
        bool rejected = false; try { controller.Restore(saved); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && controller.BuildFrame(1920, 1080).items[0].mask?.identity.persistentId == image.identity.persistentId, "Missing candidate mask replaced current character.");
        var copy = CharacterAssetCopy.Copy(source); copy.poses[0].parts[0].surface.cropWidth = 1;
        Check(part.surface.cropWidth == .5, "Character copy retained mutable composition values.");
    }

    private static IEnumerable<NovelImagePlan> Walk(NovelImagePlan root)
    {
        var seen = new HashSet<NovelImagePlan>(ReferenceEqualityComparer.Instance); var pending = new Stack<NovelImagePlan>(); pending.Push(root);
        while (pending.Count > 0)
        {
            var item = pending.Pop(); if (!seen.Add(item)) continue; yield return item;
            if (item is NovelProcessImage process) { pending.Push(process.source); pending.Push(process.previous); }
            else if (item is NovelStackImage stack) foreach (var child in stack.layers) pending.Push(child);
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

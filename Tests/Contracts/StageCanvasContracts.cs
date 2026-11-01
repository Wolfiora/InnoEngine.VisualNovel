using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.StageEditing;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Stage.Composition;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class StageCanvasContracts
{
    internal static void ProductionGeometryAndNativeTransforms(SerializationRegistry native)
    {
        var stage = new StageAsset { width = 800, height = 600, layers = [new() { id = "back", width = 800, height = 600 },
            new() { id = "front", order = 1, width = 100, height = 80, transform = new() { x = 300, y = 200, pivotX = 50, pivotY = 40, rotation = 37, scaleX = -2, scaleY = 1.5 },
                surface = new() { cropX = .25, cropWidth = .5, cropY = .1, cropHeight = .7 } }] };
        var frame = Frame(stage); var quad = frame.items.Single(item => item.id == "stage/front");
        var middle = (quad.topLeft + quad.bottomRight) / 2;
        Check(StageCanvasEditService.HitTest(frame, middle.X, middle.Y) == "front"
            && StageCanvasEditService.HitTest(frame, 20, 20) == "back" && StageCanvasEditService.HitTest(frame, -10, -10).Length == 0,
            "Canvas picking did not use real cropped mirrored rotated production quads in paint order.");
        var start = StageCanvasEditService.Capture(stage, "front");
        StageCanvasEditService.Move(stage, start, 19, -17, 8);
        Check(stage.layers[1].transform.x == 320 && stage.layers[1].transform.y == 184, "Grid move accumulated pointer rounding or moved the local pivot.");
        StageCanvasEditService.Move(stage, start, 9, 4);
        Check(stage.layers[1].transform.x == 309 && stage.layers[1].transform.y == 204, "Gesture samples accumulated their previous frame's displacement.");
        StageCanvasEditService.Scale(stage, start, 2);
        Check(stage.layers[1].transform.scaleX == -4 && stage.layers[1].transform.scaleY == 3
            && stage.layers[1].transform.x == 300 && stage.layers[1].transform.y == 200, "Uniform scale lost mirror signs or changed its authored pivot anchor.");
        StageCanvasEditService.Rotate(stage, start, 20, 15);
        Check(stage.layers[1].transform.rotation == 60 && stage.layers[1].transform.pivotX == 50
            && stage.layers[1].transform.scaleX == -2, "Relative rotation lost its captured geometry or degree snap.");
        StageCanvasEditService.Rotate(stage, start, 725);
        Check(stage.layers[1].transform.rotation == 42, "Rotation did not normalize multiple turns deterministically.");
        var snapshot = native.Deserialize<StageState>(native.Serialize(State(stage))); var restored = new StageController([]); restored.Restore(snapshot);
        Check(restored.BuildFrame(800, 600).items.Single(item => item.id == "stage/front").topLeft == Frame(stage).items.Single(item => item.id == "stage/front").topLeft,
            "Native stage restore changed the canvas-authored production transform.");
        stage.layers[1].visible = false;
        Check(StageCanvasEditService.HitTest(Frame(stage), middle.X, middle.Y) == "back", "Hidden layers remained selectable through absent production geometry.");
        stage.layers[0].transform.scaleX = 0;
        Check(StageCanvasEditService.HitTest(Frame(stage), 0, 20).Length == 0, "Degenerate geometry inverted to an identity hit region.");
    }
    internal static void FrozenConstraintsAndAtomicRejection()
    {
        var stage = new StageAsset { layers = [new() { id = "layer", width = 100, height = 100, transform = new() { x = 100, y = 200, pivotX = 50, pivotY = 50 } }] };
        var start = StageCanvasEditService.Capture(stage, "layer");
        Reject(() => StageCanvasEditService.Move(stage, start, double.NaN, 1));
        Reject(() => StageCanvasEditService.Move(stage, start, 0, 0, -1));
        Reject(() => StageCanvasEditService.Scale(stage, start, -1));
        Reject(() => StageCanvasEditService.Rotate(stage, start, double.PositiveInfinity));
        Check(stage.layers[0].transform.x == 100 && stage.layers[0].transform.scaleX == 1, "Invalid gesture partially replaced the live declaration.");
        stage.layers[0].transform.pivotX = 25;
        Reject(() => StageCanvasEditService.Move(stage, start, 10, 20));
        Check(stage.layers[0].transform.pivotX == 25 && stage.layers[0].transform.x == 100, "A stale pivot was overwritten by an old canvas origin.");
        stage.layers[0].transform.pivotX = 50; stage.width += 100;
        Reject(() => StageCanvasEditService.Rotate(stage, start, 20));
        stage.width -= 100; stage.layers[0].width = 0;
        Reject(() => StageCanvasEditService.Capture(stage, "layer"));
        stage.layers[0].width = 100; stage.layers[0].transform.scaleX = 1e7;
        var large = StageCanvasEditService.Capture(stage, "layer");
        Reject(() => StageCanvasEditService.Scale(stage, large, 1000));
        Check(stage.layers[0].transform.scaleX == 1e7, "Out-of-range scaling mutated the declaration before validation.");
        var other = StageAssetCopy.Copy(stage); other.resourceId += "-other";
        Reject(() => StageCanvasEditService.Move(other, large, 1, 1));
    }
    private static StageState State(StageAsset stage)
    {
        var controller = new StageController([]); controller.SetStage(stage); return controller.Capture();
    }
    private static StageFrame Frame(StageAsset stage)
    {
        var controller = new StageController([]); controller.SetStage(stage); return controller.BuildFrame((float)stage.width, (float)stage.height);
    }
    private static void Reject(Action action)
    {
        try { action(); } catch (Exception error) when (error is ArgumentException or InvalidOperationException) { return; }
        throw new InvalidOperationException("Invalid or stale canvas operation was accepted.");
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}

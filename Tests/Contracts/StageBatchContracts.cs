using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.StageEditing;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Stage.Composition;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class StageBatchContracts
{
    internal static void RelativeChangesAndAtomicNativeState(SerializationRegistry native)
    {
        var stage = new StageAsset { resourceId = "batch-stage", width = 800, height = 600, layers = [
            new() { id = "a", name = "First", order = 11, width = 100, height = 100, transform = new() { x = 100, y = 120, pivotX = 20, pivotY = 10, scaleX = -2, scaleY = 1.5, rotation = 15 } },
            new() { id = "b", name = "Second", order = 22, width = 100, height = 100, transform = new() { x = 300, y = 200 } },
            new() { id = "untouched", order = 33, width = 100, height = 100, transform = new() { x = 500 } }] };
        var untouched = stage.layers[2].transform; var textures = stage.textures;
        var group = Enum.GetValues<StageLayerGroup>().Last();
        int count = StageLayerBatchEditService.Apply(stage, ["b", "a"], new() { offset = new(-25, 10), scale = 1.5, rotation = 20,
            opacity = .4, color = 0x112233AA, visible = true, group = group });
        Check(count == 2 && stage.layers[0].transform is { x: 75, y: 130, scaleX: -3, scaleY: 2.25, rotation: 35, pivotX: 20, pivotY: 10, opacity: .4 }
            && stage.layers[1].transform is { x: 275, y: 210, scaleX: 1.5, rotation: 20 } && stage.layers[0].order == 11 && stage.layers[1].order == 22,
            "A relative batch lost captured placement, pivot, mirror sign or original paint order.");
        Check(stage.layers.Take(2).All(layer => layer.color == 0x112233AA && layer.group == group && layer.visible)
            && ReferenceEquals(untouched, stage.layers[2].transform) && ReferenceEquals(textures, stage.textures) && stage.layers[0].id == "a" && stage.resourceId == "batch-stage",
            "Batch appearance overrides changed an unselected layer, resource identity or borrowed references.");
        var controller = new StageController([]); controller.SetStage(stage);
        var saved = native.Deserialize<StageState>(native.Serialize(controller.Capture()));
        var restored = new StageController([]); restored.Restore(saved);
        Check(restored.BuildFrame(800, 600).items.Single(static item => item.id == "stage/a").topLeft == controller.BuildFrame(800, 600).items.Single(static item => item.id == "stage/a").topLeft,
            "Batch-authored geometry bypassed the ordinary native runtime snapshot.");
        stage.layers[1].transform.scaleX = 1e7; var first = stage.layers[0].transform; double before = first.x;
        Reject(() => StageLayerBatchEditService.Apply(stage, ["a", "b"], new() { offset = new(10, 20), scale = 1000 }));
        Check(ReferenceEquals(first, stage.layers[0].transform) && first.x == before && stage.layers[1].transform.scaleX == 1e7,
            "A late constraint failure partially published an earlier selected layer.");
        Reject(() => StageLayerBatchEditService.Apply(stage, ["a", "missing"], new() { visible = false }));
        Reject(() => StageLayerBatchEditService.Apply(stage, ["a", "a"], new() { color = 0 }));
        Reject(() => StageLayerBatchEditService.Apply(stage, ["a"], new() { opacity = double.NaN }));
        Reject(() => StageLayerBatchEditService.Apply(stage, ["a"], new() { scale = -1 }));
        Check(stage.layers[0].visible && stage.layers[0].color == 0x112233AA, "Rejected selection or appearance parameters modified existing stage data.");
    }
    private static void Reject(Action operation)
    { try { operation(); } catch (ArgumentException) { return; } catch (InvalidOperationException) { return; } throw new InvalidOperationException("An invalid stage batch was accepted."); }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}

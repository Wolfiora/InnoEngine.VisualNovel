using System;
using System.Linq;
using InnoEngine.Mathematics;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Stage.Composition;
using Inno.VisualNovel.Authoring.Diagnostics;

namespace Inno.VisualNovel.Authoring.StageEditing;

/// <summary>Edits native stage transforms using production geometry and engine matrix operations.</summary>
public static class StageCanvasEditService
{
    /// <summary>Finds the frontmost visible stage quad under a design-space point, including rotation, mirroring and source cropping.</summary>
    /// <param name="frame">The actual production stage extraction in back-to-front order.</param>
    /// <param name="x">The horizontal pointer coordinate.</param>
    /// <param name="y">The vertical pointer coordinate.</param>
    /// <returns>The stage-local layer identity, or empty when no editable quad contains the point.</returns>
    public static string HitTest(
        StageFrame frame,
        double x,
        double y
    ) {
        Check(x); Check(y);
        for (int index = frame.items.Count - 1; index >= 0; index--)
        {
            var item = frame.items[index];
            if (!item.id.StartsWith("stage/", StringComparison.Ordinal) || (item.color & 255) == 0) continue;
            var matrix = Matrix.identity;
            matrix.m11 = item.topRight.X - item.topLeft.X; matrix.m21 = item.topRight.Y - item.topLeft.Y;
            matrix.m12 = item.bottomLeft.X - item.topLeft.X; matrix.m22 = item.bottomLeft.Y - item.topLeft.Y;
            matrix.m14 = item.topLeft.X; matrix.m24 = item.topLeft.Y;
            if (Math.Abs(Matrix.Determinant(matrix)) < .000001f) continue;
            Vector2 local = Vector2.Transform(new((float)x, (float)y), Matrix.Invert(matrix));
            if (new Rect(0, 0, 1, 1).Contains(local)) return item.id[6..];
        }
        return string.Empty;
    }
    /// <summary>Freezes the selected layer's current transform without retaining the live authored object.</summary>
    /// <param name="stage">The detached native stage draft.</param>
    /// <param name="layerId">The exact stage-local layer identity.</param>
    /// <returns>A gesture origin shared by pointer, keyboard and alignment operations.</returns>
    public static StageCanvasTransformStart Capture(
        StageAsset stage,
        string layerId
    ) {
        var layer = stage.layers.Single(item => item.id == layerId);
        Check(stage.width); Check(stage.height); Check(layer.width); Check(layer.height);
        Validate(layer.transform);
        if (stage.width <= 0 || stage.height <= 0 || layer.width <= 0 || layer.height <= 0)
            throw new AuthoringValidationException($"Editable stages and layers require positive width and height.");
        return new(stage, layer);
    }
    /// <summary>Moves from the frozen origin without accumulating per-frame rounding or changing scale, pivot or rotation.</summary>
    /// <param name="stage">The current detached document candidate.</param>
    /// <param name="start">The beginning of this gesture.</param>
    /// <param name="deltaX">The complete horizontal pointer displacement.</param>
    /// <param name="deltaY">The complete vertical pointer displacement.</param>
    /// <param name="grid">The placement interval in design pixels, or zero to disable snapping.</param>
    public static void Move(
        StageAsset stage,
        StageCanvasTransformStart start,
        double deltaX,
        double deltaY,
        double grid = 0
    ) {
        Check(deltaX); Check(deltaY); CheckGrid(grid);
        var layer = Resolve(stage, start); var result = start.CopyTransform();
        result.x = Snap(start.x + deltaX, grid); result.y = Snap(start.y + deltaY, grid);
        Apply(layer, result);
    }
    /// <summary>Scales both axes about the unchanged authored pivot while preserving existing mirror signs.</summary>
    /// <param name="stage">The current detached document candidate.</param>
    /// <param name="start">The beginning of this gesture.</param>
    /// <param name="factor">A finite positive multiplier of the captured scale, from 0.001 to 1000.</param>
    public static void Scale(
        StageAsset stage,
        StageCanvasTransformStart start,
        double factor
    ) {
        if (!double.IsFinite(factor) || factor is < .001 or > 1000) throw new ArgumentOutOfRangeException(nameof(factor));
        var layer = Resolve(stage, start); var result = start.CopyTransform();
        result.scaleX *= factor; result.scaleY *= factor;
        Apply(layer, result);
    }
    /// <summary>Rotates about the unchanged authored pivot using a complete relative angle and optional degree snapping.</summary>
    /// <param name="stage">The current detached document candidate.</param>
    /// <param name="start">The beginning of this gesture.</param>
    /// <param name="deltaDegrees">The pointer angle relative to its initial angle.</param>
    /// <param name="angleStep">The degree interval, or zero for free rotation.</param>
    public static void Rotate(
        StageAsset stage,
        StageCanvasTransformStart start,
        double deltaDegrees,
        double angleStep = 0
    ) {
        Check(deltaDegrees); CheckGrid(angleStep);
        var layer = Resolve(stage, start); var result = start.CopyTransform();
        result.rotation = Math.IEEERemainder(Snap(result.rotation + deltaDegrees, angleStep), 360);
        Apply(layer, result);
    }
    private static StageLayer Resolve(
        StageAsset stage,
        StageCanvasTransformStart start
    ) {
        var layer = stage.layers.Single(item => item.id == start.layerId); var initial = start.CopyTransform();
        if (stage.resourceId != start.resourceId || stage.width != start.width || stage.height != start.height
            || layer.width != start.layerWidth || layer.height != start.layerHeight
            || layer.transform.pivotX != initial.pivotX || layer.transform.pivotY != initial.pivotY)
            throw new AuthoringValidationException($"Stage dimensions, layer geometry or its pivot changed. Restart the canvas gesture.");
        return layer;
    }
    private static void Apply(
        StageLayer layer,
        StageTransform transform
    ) { Validate(transform); layer.transform = transform; }
    private static void Validate(StageTransform transform)
    {
        foreach (double value in new[] { transform.x, transform.y, transform.pivotX, transform.pivotY, transform.scaleX, transform.scaleY, transform.rotation }) Check(value);
        if (!double.IsFinite(transform.opacity) || transform.opacity is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(transform));
    }
    private static void Check(double value)
    {
        if (!double.IsFinite(value) || Math.Abs(value) > 1e7) throw new ArgumentOutOfRangeException(nameof(value), "Stage canvas coordinates must be finite and bounded.");
    }
    private static void CheckGrid(double grid)
    {
        if (!double.IsFinite(grid) || grid is < 0 or > 10000) throw new ArgumentOutOfRangeException(nameof(grid));
    }
    private static double Snap(
        double value,
        double grid
    ) => grid == 0 ? value : Math.Round(value / grid, MidpointRounding.AwayFromZero) * grid;
}

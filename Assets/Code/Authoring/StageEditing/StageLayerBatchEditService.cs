using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Stages;

namespace Inno.VisualNovel.Authoring.StageEditing;

/// <summary>Prepares all selected layer changes through native stage copies and existing canvas constraints before publishing any mutation.</summary>
public static class StageLayerBatchEditService
{
    /// <summary>Applies one atomic authoring operation to a detached native stage draft without changing identities, references or unselected layers.</summary>
    /// <param name="stage">The current authoring transaction candidate.</param>
    /// <param name="layerIds">One to 10000 distinct existing stage-local identities.</param>
    /// <param name="change">Relative geometry and explicitly enabled appearance overrides.</param>
    /// <returns>The number of selected layers processed; the caller records one engine History transaction.</returns>
    public static int Apply(
        StageAsset stage,
        IEnumerable<string> layerIds,
        StageLayerBatchChange change
    ) {
        ArgumentNullException.ThrowIfNull(stage); ArgumentNullException.ThrowIfNull(layerIds); ArgumentNullException.ThrowIfNull(change);
        string[] ids = layerIds.Take(10001).ToArray();
        if (ids.Length is < 1 or > 10000 || ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
            throw new ArgumentException("Select distinct existing stage layer identities before applying a batch.", nameof(layerIds));
        if (!float.IsFinite(change.offset.x) || !float.IsFinite(change.offset.y) || !double.IsFinite(change.scale) || change.scale is < .001 or > 1000
            || !double.IsFinite(change.rotation) || change.opacity is { } opacity && (!double.IsFinite(opacity) || opacity is < 0 or > 1)
            || change.group is { } group && !Enum.IsDefined(group)) throw new ArgumentException("Stage batch changes require finite geometry, valid alpha and a supported paint band.", nameof(change));
        var byId = stage.layers.ToDictionary(static layer => layer.id, StringComparer.Ordinal);
        if (ids.Any(id => !byId.ContainsKey(id))) throw new InvalidOperationException("A selected stage layer was removed; review the batch selection again.");
        var copy = StageAssetCopy.Copy(stage); var requested = ids.ToHashSet(StringComparer.Ordinal);
        foreach (StageLayer layer in copy.layers.Where(layer => requested.Contains(layer.id)))
        {
            var geometry = new StageAsset { resourceId = copy.resourceId, width = copy.width, height = copy.height, layers = [layer] };
            if (change.offset.x != 0 || change.offset.y != 0) StageCanvasEditService.Move(geometry, StageCanvasEditService.Capture(geometry, layer.id), change.offset.x, change.offset.y);
            if (change.scale != 1) StageCanvasEditService.Scale(geometry, StageCanvasEditService.Capture(geometry, layer.id), change.scale);
            if (change.rotation != 0) StageCanvasEditService.Rotate(geometry, StageCanvasEditService.Capture(geometry, layer.id), change.rotation);
            if (change.opacity is { } alpha) layer.transform.opacity = alpha;
            if (change.visible is { } visible) layer.visible = visible;
            if (change.color is { } color) layer.color = color;
            if (change.group is { } band) layer.group = band;
        }
        foreach (StageLayer prepared in copy.layers.Where(layer => requested.Contains(layer.id)))
        {
            StageLayer target = byId[prepared.id]; target.transform = prepared.transform.Copy(); target.visible = prepared.visible;
            target.color = prepared.color; target.group = prepared.group;
        }
        return ids.Length;
    }
}

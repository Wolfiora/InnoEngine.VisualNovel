using Inno.VisualNovel.Model.Stages;

namespace Inno.VisualNovel.Authoring.StageEditing;

/// <summary>Retains a detached layer transform and its geometric constraints at the beginning of a canvas gesture.</summary>
public sealed class StageCanvasTransformStart
{
    private readonly StageTransform m_transform;
    internal StageCanvasTransformStart(
        StageAsset stage,
        StageLayer layer
    ) {
        resourceId = stage.resourceId; width = stage.width; height = stage.height; layerId = layer.id;
        layerWidth = layer.width; layerHeight = layer.height; m_transform = layer.transform.Copy();
    }
    /// <summary>Gets the exact stage-local layer identity.</summary>
    public string layerId { get; }
    /// <summary>Gets the captured horizontal pivot position in design pixels.</summary>
    public double x => m_transform.x;
    /// <summary>Gets the captured vertical pivot position in design pixels.</summary>
    public double y => m_transform.y;
    internal string resourceId { get; }
    internal double width { get; }
    internal double height { get; }
    internal double layerWidth { get; }
    internal double layerHeight { get; }
    internal StageTransform CopyTransform() => m_transform.Copy();
}

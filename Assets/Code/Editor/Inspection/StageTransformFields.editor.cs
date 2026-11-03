using Inno.VisualNovel.Model.Stages;

namespace Inno.VisualNovel.Editor.Inspection;

internal static class StageTransformFields
{
    internal static bool Draw(
        StoryInspectorFields fields,
        string id,
        StageTransform transform
    ) {
        double x = transform.x, y = transform.y, scaleX = transform.scaleX, scaleY = transform.scaleY, rotation = transform.rotation, opacity = transform.opacity;
        bool changed = fields.Number(id + "/x", "Position X", ref x);
        changed |= fields.Number(id + "/y", "Position Y", ref y);
        changed |= fields.Number(id + "/sx", "Horizontal scale", ref scaleX, -1000, 1000);
        changed |= fields.Number(id + "/sy", "Vertical scale", ref scaleY, -1000, 1000);
        changed |= fields.Number(id + "/rotation", "Rotation angle", ref rotation, -1000000, 1000000);
        changed |= fields.Number(id + "/opacity", "Opacity", ref opacity, 0, 1);
        double pivotX = transform.pivotX, pivotY = transform.pivotY;
        changed |= fields.Number(id + "/pivot-x", "Local pivot X (pixels)", ref pivotX);
        changed |= fields.Number(id + "/pivot-y", "Local pivot Y (pixels)", ref pivotY);
        transform.pivotX = pivotX; transform.pivotY = pivotY;
        transform.x = x; transform.y = y; transform.scaleX = scaleX; transform.scaleY = scaleY; transform.rotation = rotation; transform.opacity = opacity;
        return changed;
    }
}

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
        bool changed = fields.Number(id + "/x", fields.Localize("横向位置"), ref x);
        changed |= fields.Number(id + "/y", fields.Localize("纵向位置"), ref y);
        changed |= fields.Number(id + "/sx", fields.Localize("水平缩放"), ref scaleX, -1000, 1000);
        changed |= fields.Number(id + "/sy", fields.Localize("垂直缩放"), ref scaleY, -1000, 1000);
        changed |= fields.Number(id + "/rotation", fields.Localize("旋转角度"), ref rotation, -1000000, 1000000);
        changed |= fields.Number(id + "/opacity", fields.Localize("不透明度"), ref opacity, 0, 1);
        double pivotX = transform.pivotX, pivotY = transform.pivotY;
        changed |= fields.Number(id + "/pivot-x", fields.Localize("本地枢轴 X（像素）"), ref pivotX);
        changed |= fields.Number(id + "/pivot-y", fields.Localize("本地枢轴 Y（像素）"), ref pivotY);
        transform.pivotX = pivotX; transform.pivotY = pivotY;
        transform.x = x; transform.y = y; transform.scaleX = scaleX; transform.scaleY = scaleY; transform.rotation = rotation; transform.opacity = opacity;
        return changed;
    }
}

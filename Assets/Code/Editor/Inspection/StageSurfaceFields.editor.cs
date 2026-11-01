using System;
using System.Linq;
using InnoEngine.Rendering;
using InnoEditor.ImGui;
using Inno.VisualNovel.Model.Stages;

namespace Inno.VisualNovel.Editor.Inspection;

internal static class StageSurfaceFields
{
    internal static bool Draw(
        StoryInspectorFields fields,
        string id,
        StageSurface surface,
        TextureAsset[] textures,
        ref int mask
    ) {
        if (!ImGui.CollapsingHeader(fields.Caption("源图、裁剪、混合与遮罩##" + id, fields.Localize("源图、裁剪、混合与遮罩##") + id))) return false;
        bool changed = false;
        double x = surface.sourceX, y = surface.sourceY, width = surface.sourceWidth, height = surface.sourceHeight;
        changed |= fields.Number(id + "/source-x", fields.Localize("源图左边（0～1）"), ref x, 0, 1);
        changed |= fields.Number(id + "/source-y", fields.Localize("源图上边（0～1）"), ref y, 0, 1);
        changed |= fields.Number(id + "/source-w", fields.Localize("源图宽度（0～1）"), ref width, 0, 1 - x);
        changed |= fields.Number(id + "/source-h", fields.Localize("源图高度（0～1）"), ref height, 0, 1 - y);
        surface.sourceX = x; surface.sourceY = y; surface.sourceWidth = Math.Min(width, 1 - x); surface.sourceHeight = Math.Min(height, 1 - y);
        x = surface.cropX; y = surface.cropY; width = surface.cropWidth; height = surface.cropHeight;
        changed |= fields.Number(id + "/crop-x", fields.Localize("保留区域左边（0～1）"), ref x, 0, 1);
        changed |= fields.Number(id + "/crop-y", fields.Localize("保留区域上边（0～1）"), ref y, 0, 1);
        changed |= fields.Number(id + "/crop-w", fields.Localize("保留区域宽度（0～1）"), ref width, 0, 1 - x);
        changed |= fields.Number(id + "/crop-h", fields.Localize("保留区域高度（0～1）"), ref height, 0, 1 - y);
        surface.cropX = x; surface.cropY = y; surface.cropWidth = Math.Min(width, 1 - x); surface.cropHeight = Math.Min(height, 1 - y);
        StageBlendMode blend = surface.blend; StageMaskMode mode = surface.maskMode;
        changed |= fields.SelectEnum(id + "/blend", "混合方式", ref blend); surface.blend = blend;
        string selection = mask.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (fields.Select(id + "/mask", fields.Localize("遮罩图像"), ref selection, new[] { ("-1", fields.Localize("无遮罩")) }.Concat(textures.Select((image, index) =>
            (index.ToString(System.Globalization.CultureInfo.InvariantCulture), index + " · " + image.name)))))
        { mask = int.Parse(selection, System.Globalization.CultureInfo.InvariantCulture); changed = true; }
        changed |= fields.SelectEnum(id + "/mask-mode", "遮罩通道", ref mode); surface.maskMode = mode;
        return changed;
    }
}

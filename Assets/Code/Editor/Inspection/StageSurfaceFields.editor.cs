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
        if (!ImGui.CollapsingHeader("Image source, crop, blend and mask##" + id)) return false;
        bool changed = false;
        double x = surface.sourceX, y = surface.sourceY, width = surface.sourceWidth, height = surface.sourceHeight;
        changed |= fields.Number(id + "/source-x", "Source left (0–1)", ref x, 0, 1);
        changed |= fields.Number(id + "/source-y", "Source top (0–1)", ref y, 0, 1);
        changed |= fields.Number(id + "/source-w", "Source width (0–1)", ref width, 0, 1 - x);
        changed |= fields.Number(id + "/source-h", "Source height (0–1)", ref height, 0, 1 - y);
        surface.sourceX = x; surface.sourceY = y; surface.sourceWidth = Math.Min(width, 1 - x); surface.sourceHeight = Math.Min(height, 1 - y);
        x = surface.cropX; y = surface.cropY; width = surface.cropWidth; height = surface.cropHeight;
        changed |= fields.Number(id + "/crop-x", "Retained region left (0–1)", ref x, 0, 1);
        changed |= fields.Number(id + "/crop-y", "Retained region top (0–1)", ref y, 0, 1);
        changed |= fields.Number(id + "/crop-w", "Retained region width (0–1)", ref width, 0, 1 - x);
        changed |= fields.Number(id + "/crop-h", "Retained region height (0–1)", ref height, 0, 1 - y);
        surface.cropX = x; surface.cropY = y; surface.cropWidth = Math.Min(width, 1 - x); surface.cropHeight = Math.Min(height, 1 - y);
        StageBlendMode blend = surface.blend; StageMaskMode mode = surface.maskMode;
        changed |= fields.SelectEnum(id + "/blend", "Blend mode", ref blend); surface.blend = blend;
        string selection = mask.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (fields.Select(id + "/mask", "Mask image", ref selection, new[] { ("-1", "No mask") }.Concat(textures.Select((image, index) =>
            (index.ToString(System.Globalization.CultureInfo.InvariantCulture), index + " · " + image.name)))))
        { mask = int.Parse(selection, System.Globalization.CultureInfo.InvariantCulture); changed = true; }
        changed |= fields.SelectEnum(id + "/mask-mode", "Mask channel", ref mode); surface.maskMode = mode;
        return changed;
    }
}

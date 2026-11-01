using System;
using System.Numerics;
using InnoEditor.ImGui;
using Inno.VisualNovel.Editor.Localization;

namespace Inno.VisualNovel.Editor.Inspection;

internal static class NovelColorField
{
    internal static bool Draw(
        EditorTextView text,
        string id,
        string label,
        ref uint color
    ) {
        Vector4 linear = new(Decode(color >> 24), Decode(color >> 16), Decode(color >> 8), (color & 255) / 255f);
        if (!ImGui.ColorEditLinear4(text.Label(label + "###vn-color/" + id), ref linear)) return false;
        color = Encode(linear.X) << 24 | Encode(linear.Y) << 16 | Encode(linear.Z) << 8 | (uint)Math.Round(Math.Clamp(linear.W, 0, 1) * 255);
        return true;
    }
    private static float Decode(uint packed)
    {
        float value = (packed & 255) / 255f;
        return value <= 0.04045f ? value / 12.92f : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
    }
    private static uint Encode(float linear)
    {
        float value = Math.Clamp(linear, 0, 1);
        return (uint)Math.Round((value <= 0.0031308f ? value * 12.92f : 1.055f * MathF.Pow(value, 1 / 2.4f) - 0.055f) * 255);
    }
}

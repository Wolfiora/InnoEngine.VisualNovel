using System.Collections.Generic;
using Inno.VisualNovel.Model.Performance;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class OriginalMotionLibrary
{
    internal static IReadOnlyList<PerformanceClipAsset> Create() => [
        Clip("enter-left", "从左入场", 0.5, false, Track("actor", PerformanceProperty.X, (-300, 0), (0, 0.5))),
        Clip("enter-right", "从右入场", 0.5, false, Track("actor", PerformanceProperty.X, (300, 0), (0, 0.5))),
        Clip("enter-bottom", "从下入场", 0.5, false, Track("actor", PerformanceProperty.Y, (150, 0), (0, 0.5))),
        Clip("fade-in", "淡入", 0.45, false, Track("actor", PerformanceProperty.Opacity, (0, 0), (1, 0.45))),
        Clip("fade-out", "淡出", 0.45, false, Track("actor", PerformanceProperty.Opacity, (1, 0), (0, 0.45))),
        Clip("exit-left", "向左退场", 0.5, false, Track("actor", PerformanceProperty.X, (0, 0), (-300, 0.5))),
        Clip("exit-right", "向右退场", 0.5, false, Track("actor", PerformanceProperty.X, (0, 0), (300, 0.5))),
        Clip("exit-bottom", "向下退场", 0.5, false, Track("actor", PerformanceProperty.Y, (0, 0), (150, 0.5))),
        Clip("bounce", "轻跳强调", 0.4, false, Track("actor", PerformanceProperty.Y, (0, 0), (-28, 0.18), (0, 0.4))),
        Clip("emphasis", "缩放强调", 0.45, false, Track("actor", PerformanceProperty.ScaleX, (1, 0), (1.07, 0.2), (1, 0.45)), Track("actor", PerformanceProperty.ScaleY, (1, 0), (1.07, 0.2), (1, 0.45))),
        Clip("shake", "短促抖动", 0.4, false, Track("actor", PerformanceProperty.ShakeX, (0, 0), (12, 0.06), (-12, 0.12), (8, 0.2), (-8, 0.28), (0, 0.4))),
        Clip("nod", "点头", 0.35, false, Track("actor", PerformanceProperty.Y, (0, 0), (12, 0.15), (0, 0.35))),
        Clip("float", "轻轻漂浮", 3, true, Track("actor", PerformanceProperty.Y, (0, 0), (-12, 1.5), (0, 3))),
        Clip("breathe", "细微呼吸", 3.2, true, Track("actor", PerformanceProperty.ScaleY, (1, 0), (1.015, 1.6), (1, 3.2))),
        Clip("sway", "柔和摇摆", 4, true, Track("actor", PerformanceProperty.Rotation, (-1.2, 0), (1.2, 2), (-1.2, 4))),
        Clip("camera-push", "镜头推进", 1.2, false, Track("camera", PerformanceProperty.ScaleX, (1, 0), (1.12, 1.2))),
        Clip("camera-pull", "镜头拉远", 1.2, false, Track("camera", PerformanceProperty.ScaleX, (1.12, 0), (1, 1.2))),
        Clip("camera-pan", "镜头横移", 1.2, false, Track("camera", PerformanceProperty.X, (0, 0), (120, 1.2))),
        Clip("camera-shake", "镜头震动", 0.45, false, Track("camera", PerformanceProperty.ShakeX, (0, 0), (10, 0.08), (-10, 0.16), (6, 0.24), (-6, 0.32), (0, 0.45))),
        Clip("dim", "压暗角色", 0.3, false, Track("actor", PerformanceProperty.Opacity, (1, 0), (0.5, 0.3)))
    ];
    private static PerformanceClipAsset Clip(string id, string name, double duration, bool loop, params PerformanceTrack[] tracks) => new()
    { resourceId = "builtin.motion." + id, displayName = name, duration = duration, loop = loop, tracks = tracks,
        markers = [new() { id = "start", name = "开始", time = 0 }, new() { id = "middle", name = "中点", time = duration / 2 }, new() { id = "end", name = "结束", time = duration }] };
    private static PerformanceTrack Track(string target, PerformanceProperty property, params (double value, double time)[] values)
    {
        var keys = new PerformanceKeyframe[values.Length];
        for (int index = 0; index < values.Length; index++) keys[index] = new() { time = values[index].time, value = values[index].value, interpolation = PerformanceInterpolation.Smooth };
        return new() { target = target, property = property, keys = keys };
    }
}

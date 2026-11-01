using System;
using System.Collections.Generic;
using Inno.VisualNovel.Model.Performance;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class OriginalImageEffects
{
    internal static IReadOnlyList<VisualEffectAsset> Transitions() => [
        Make("fade-black", "淡出至黑", "经由黑色的两阶段淡化，适合章节切换。", new() { kind = TransitionKind.Fade }),
        Make("fade-white", "淡出至白", "经由白色的两阶段淡化；关闭闪烁或减少动态效果时直接完成。", new() { kind = TransitionKind.Fade, color = 0xFFFFFFFF }),
        Make("dissolve", "交叉溶解", "旧画面与新画面的连续透明度混合。", new() { kind = TransitionKind.Dissolve }),
        Make("wipe-right", "向右擦除", "柔和边缘从左向右揭示新画面。", new() { kind = TransitionKind.Wipe, angle = 0 }),
        Make("wipe-down", "向下擦除", "柔和边缘从上向下揭示新画面。", new() { kind = TransitionKind.Wipe, angle = 90 }),
        Make("wipe-diagonal", "斜向擦除", "沿对角线逐渐揭示新画面。", new() { kind = TransitionKind.Wipe, angle = 35 }),
        Make("mask-noise", "颗粒溶解", "固定随机种子的颗粒阈值遮罩。", new() { kind = TransitionKind.Mask, mask = TransitionMask.Noise, softness = .08 }),
        Make("mask-checker", "棋盘展开", "交错格子的阈值遮罩，不需要外部遮罩资源。", new() { kind = TransitionKind.Mask, mask = TransitionMask.Checker }),
        Make("mask-blinds", "百叶窗", "水平条带逐步展开。", new() { kind = TransitionKind.Mask, mask = TransitionMask.Blinds }),
        Make("slide-left", "向左推移", "旧画面退出，新画面从右侧推入。", new() { kind = TransitionKind.Slide, angle = 180 }),
        Make("iris-open", "虹膜展开", "从自定义中心向外揭示新画面。", new() { kind = TransitionKind.Iris }),
        Make("iris-close", "虹膜收拢", "从外侧向中心揭示新画面。", new() { kind = TransitionKind.Iris, reverse = true })
    ];
    internal static IReadOnlyList<VisualEffectAsset> Screens()
    {
        string[] names = ["柔焦模糊", "柔和暗角", "灰度", "暖色调", "像素化", "闪白", "电影遮幅", "怀旧棕褐", "反相", "色差"];
        string[] descriptions = ["可编辑半径的双向高斯模糊。", "可编辑半径与边缘柔度的暗角。", "按亮度转换为灰度。", "可编辑饱和度、亮度与对比度。", "以设计像素为单位的像素块。", "按强度叠加闪白；尊重玩家禁用闪烁设置。", "可编辑比例与颜色的上下遮幅。", "保留透明度的棕褐着色。", "保留透明度的颜色反相。", "可编辑横向红蓝通道偏移。"];
        var result = new List<VisualEffectAsset>();
        foreach (ScreenEffectKind kind in Enum.GetValues<ScreenEffectKind>())
        {
            var definition = new ScreenEffectDefinition { kind = kind };
            if (kind == ScreenEffectKind.ColorGrade) { definition.saturation = .8; definition.brightness = .03; definition.contrast = 1.08; }
            if (kind == ScreenEffectKind.Letterbox) definition.color = 0x000000FF;
            result.Add(new() { kind = VisualEffectKind.Screen, resourceId = "builtin.screen." + kind.ToString().ToLowerInvariant(), displayName = names[(int)kind], description = descriptions[(int)kind], screen = definition });
        }
        return result;
    }
    private static VisualEffectAsset Make(
        string id,
        string title,
        string description,
        TransitionDefinition definition
    ) => new() { kind = VisualEffectKind.Transition, resourceId = "builtin.transition." + id, displayName = title, description = description, transition = definition };
}

using System.Collections.Generic;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class EditorEnglishStageDesigner
{
    internal static IReadOnlyList<EditorEnglishEntry> entries { get; } = new EditorEnglishEntry[]
    {
        new("literal", "画布工具", "Canvas tools"),
        new("literal", "移动图层", "Move layer"),
        new("literal", "等比缩放", "Uniform scale"),
        new("literal", "旋转", "Rotate"),
        new("literal", "舞台吸附", "Stage snapping"),
        new("literal", "显示构图安全区", "Show composition safe area"),
        new("literal", "安全参考边距（px）", "Safe area margin (px)"),
        new("literal", "拖动图层；方向键微调 1 px，Shift 为 10 px，Alt 暂停吸附，Esc 取消当前拖动。", "Drag a layer; arrow keys move 1 px, Shift moves 10 px, Alt suspends snapping, and Esc cancels the drag."),
        new("literal", "拖动右下角方形柄，相对作者枢轴等比缩放，保留镜像；Esc 取消。", "Drag the bottom-right square to scale uniformly around the authored pivot, preserving mirroring; Esc cancels."),
        new("literal", "拖动上方圆形柄旋转；吸附时每 15°，Alt 自由旋转，Esc 取消。", "Drag the upper circle to rotate in 15° snap increments; Alt enables free rotation and Esc cancels."),
        new("literal", "微调舞台图层", "Nudge stage layer"),
        new("literal", "移动舞台图层", "Move stage layer"),
        new("literal", "缩放舞台图层", "Scale stage layer"),
        new("literal", "旋转舞台图层", "Rotate stage layer"),
        new("literal", "VN 舞台设计 · Stage Designer", "VN Stage Designer"),
        new("literal", "舞台", "Stage"),
        new("literal", "舞台名称", "Stage name"),
        new("literal", "添加纯色图层", "Add solid-color layer"),
        new("literal", "添加舞台图层", "Add stage layer"),
        new("literal", "图层名称", "Layer name"),
        new("literal", "图层宽度", "Layer width"),
        new("literal", "图层高度", "Layer height"),
        new("literal", "视差系数", "Parallax factor"),
        new("literal", "使用所选图像作为图层遮罩", "Use selected image as layer mask"),
        new("literal", "图层颜色", "Layer color"),
        new("literal", "可见", "Visible"),
        new("literal", "设置舞台图像", "Set stage image"),
        new("literal", "移除此图层", "Remove this layer"),
        new("literal", "移除舞台图层", "Remove stage layer"),
        new("literal", "编辑舞台", "Edit stage"),
        new("literal", "移动剧情节点", "Move story node"),
        new("literal", "连接剧情节点", "Connect story nodes"),
    };
}

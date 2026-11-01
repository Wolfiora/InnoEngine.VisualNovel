using System.Collections.Generic;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class EditorEnglishUiAnimation
{
    internal static IReadOnlyList<EditorEnglishEntry> entries { get; } = new EditorEnglishEntry[]
    {
        new("literal", "显隐与交互动画##", "Visibility and interaction animations##"),
        new("literal", "时长（秒）", "Duration (seconds)"),
        new("literal", "延迟（秒）", "Delay (seconds)"),
        new("literal", "缓动", "Easing"),
        new("literal", "相对透明度", "Relative opacity"),
        new("literal", "相对横移", "Relative horizontal offset"),
        new("literal", "相对纵移", "Relative vertical offset"),
        new("literal", "相对缩放", "Relative scale"),
        new("literal", "淡化##", "Fade##"),
        new("literal", "下移淡化##", "Slide down and fade##"),
        new("literal", "轻微缩放##", "Subtle scale##"),
        new("literal", "等待动画条件（空为常开）", "Waiting animation condition (empty for always active)"),
        new("literal", "显现从指定姿态回到正常姿态；隐藏向指定姿态运动。悬停、焦点、按下可平滑返回；等待动画往返循环。布局和文字换行保持不变，隐藏立即释放输入。玩家减少动态效果时停用动画。", "Enter animations move from the specified pose to normal; exits move toward it. Hover, focus and pressed animations reverse smoothly. Waiting animations loop back and forth. Layout and text wrapping remain stable, and hidden controls release input immediately. Reduced motion disables animations."),
        new("literal", "入场 / 显现", "Enter / show"),
        new("literal", "退场 / 隐藏", "Exit / hide"),
        new("literal", "悬停", "Hover"),
        new("literal", "焦点", "Focus"),
        new("literal", "按下", "Pressed"),
        new("literal", "等待循环", "Waiting loop")
    };
}

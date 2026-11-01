using System;
using System.Collections.Generic;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Model.Interface.Animation;

namespace Inno.VisualNovel.Editor.ScreenDesigner;

internal sealed class UiAnimationFields(StoryInspectorFields fields)
{
    internal bool Draw(
        string id,
        UiAnimationDefinition animation,
        IEnumerable<(string id, string label)> conditions
    ) {
        if (!ImGui.CollapsingHeader(fields.Caption("显隐与交互动画##" + id, fields.Localize("显隐与交互动画##") + id))) return false;
        bool changed = false;
        foreach (var kind in Enum.GetValues<UiAnimationEvent>())
        {
            string key = id + "/animation/" + kind;
            var motion = animation.GetMotion(kind);
            bool enabled = motion.enabled;
            changed |= fields.Toggle(key + "/enabled", Label(kind), ref enabled); motion.enabled = enabled;
            if (!enabled) continue;
            double duration = motion.duration, delay = motion.delay, opacity = motion.pose.opacity, x = motion.pose.x, y = motion.pose.y, scale = motion.pose.scale;
            changed |= fields.Number(key + "/duration", fields.Localize("时长（秒）"), ref duration, 0, 30); motion.duration = duration;
            changed |= fields.Number(key + "/delay", fields.Localize("延迟（秒）"), ref delay, 0, 30); motion.delay = delay;
            var easing = motion.easing; changed |= fields.SelectEnum(key + "/easing", "缓动", ref easing); motion.easing = easing;
            changed |= fields.Number(key + "/opacity", fields.Localize("相对透明度"), ref opacity, 0, 1); motion.pose.opacity = (float)opacity;
            changed |= fields.Number(key + "/x", fields.Localize("相对横移"), ref x, -10000, 10000); motion.pose.x = (float)x;
            changed |= fields.Number(key + "/y", fields.Localize("相对纵移"), ref y, -10000, 10000); motion.pose.y = (float)y;
            changed |= fields.Number(key + "/scale", fields.Localize("相对缩放"), ref scale, .1, 4); motion.pose.scale = (float)scale;
            if (ImGui.SmallButton(fields.Caption("淡化##" + key, fields.Localize("淡化##") + key))) { fields.Finish(); motion.pose = new() { opacity = 0 }; changed = true; }
            ImGui.SameLine();
            if (ImGui.SmallButton(fields.Caption("下移淡化##" + key, fields.Localize("下移淡化##") + key))) { fields.Finish(); motion.pose = new() { opacity = 0, y = 24 }; changed = true; }
            ImGui.SameLine();
            if (ImGui.SmallButton(fields.Caption("轻微缩放##" + key, fields.Localize("轻微缩放##") + key))) { fields.Finish(); motion.pose = new() { scale = .96f }; changed = true; }
        }
        string waiting = animation.waitingBinding;
        changed |= fields.Select(id + "/animation/waiting-binding", fields.Localize("等待动画条件（空为常开）"), ref waiting, conditions); animation.waitingBinding = waiting;
        EditorUiSession.current.widgets.Hint(fields.Localize("显现从指定姿态回到正常姿态；隐藏向指定姿态运动。悬停、焦点、按下可平滑返回；等待动画往返循环。布局和文字换行保持不变，隐藏立即释放输入。玩家减少动态效果时停用动画。"));
        return changed;
    }
    internal static string Label(UiAnimationEvent kind) => kind switch
    {
        UiAnimationEvent.Enter => "入场 / 显现", UiAnimationEvent.Exit => "退场 / 隐藏", UiAnimationEvent.Hover => "悬停",
        UiAnimationEvent.Focus => "焦点", UiAnimationEvent.Pressed => "按下", UiAnimationEvent.Waiting => "等待循环", _ => kind.ToString()
    };
}

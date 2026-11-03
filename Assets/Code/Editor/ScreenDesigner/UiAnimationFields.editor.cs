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
        if (!ImGui.CollapsingHeader("Visibility and interaction animations##" + id)) return false;
        bool changed = false;
        foreach (var kind in Enum.GetValues<UiAnimationEvent>())
        {
            string key = id + "/animation/" + kind;
            var motion = animation.GetMotion(kind);
            bool enabled = motion.enabled;
            changed |= fields.Toggle(key + "/enabled", Label(kind), ref enabled); motion.enabled = enabled;
            if (!enabled) continue;
            double duration = motion.duration, delay = motion.delay, opacity = motion.pose.opacity, x = motion.pose.x, y = motion.pose.y, scale = motion.pose.scale;
            changed |= fields.Number(key + "/duration", "Duration (seconds)", ref duration, 0, 30); motion.duration = duration;
            changed |= fields.Number(key + "/delay", "Delay (seconds)", ref delay, 0, 30); motion.delay = delay;
            var easing = motion.easing; changed |= fields.SelectEnum(key + "/easing", "Easing", ref easing); motion.easing = easing;
            changed |= fields.Number(key + "/opacity", "Relative opacity", ref opacity, 0, 1); motion.pose.opacity = (float)opacity;
            changed |= fields.Number(key + "/x", "Relative horizontal offset", ref x, -10000, 10000); motion.pose.x = (float)x;
            changed |= fields.Number(key + "/y", "Relative vertical offset", ref y, -10000, 10000); motion.pose.y = (float)y;
            changed |= fields.Number(key + "/scale", "Relative scale", ref scale, .1, 4); motion.pose.scale = (float)scale;
            if (ImGui.SmallButton("Fade##" + key)) { fields.Finish(); motion.pose = new() { opacity = 0 }; changed = true; }
            ImGui.SameLine();
            if (ImGui.SmallButton("Slide down and fade##" + key)) { fields.Finish(); motion.pose = new() { opacity = 0, y = 24 }; changed = true; }
            ImGui.SameLine();
            if (ImGui.SmallButton("Subtle scale##" + key)) { fields.Finish(); motion.pose = new() { scale = .96f }; changed = true; }
        }
        string waiting = animation.waitingBinding;
        changed |= fields.Select(id + "/animation/waiting-binding", "Waiting animation condition (empty for always active)", ref waiting, conditions); animation.waitingBinding = waiting;
        EditorUiSession.current.widgets.Hint("Enter animations move from the specified pose to normal; exits move toward it. Hover, focus and pressed animations reverse smoothly. Waiting animations loop back and forth. Layout and text wrapping remain stable, and hidden controls release input immediately. Reduced motion disables animations.");
        return changed;
    }
    internal static string Label(UiAnimationEvent kind) => kind switch
    {
        UiAnimationEvent.Enter => "Enter / show", UiAnimationEvent.Exit => "Exit / hide", UiAnimationEvent.Hover => "Hover",
        UiAnimationEvent.Focus => "Focus", UiAnimationEvent.Pressed => "Pressed", UiAnimationEvent.Waiting => "Waiting loop", _ => kind.ToString()
    };
}

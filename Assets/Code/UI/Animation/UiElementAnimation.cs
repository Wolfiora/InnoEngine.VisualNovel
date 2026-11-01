using System;
using Inno.VisualNovel.Model.Interface.Animation;

namespace Inno.VisualNovel.UI.Animation;

internal sealed class UiElementAnimation
{
    private sealed class State
    {
        private bool m_active;
        private double m_start;
        private UiMotionPose m_from = new(), m_to = new();
        internal UiMotionPose Sample(
            UiMotionDefinition definition,
            bool active,
            double time,
            bool reduced
        ) {
            UiMotionPose current = UiMotionSampling.Blend(m_from, m_to, UiMotionSampling.Progress(definition, Math.Max(0, time - m_start)));
            if (active != m_active) { m_from = current; m_to = active && definition.enabled ? definition.pose : new(); m_start = time; m_active = active; }
            if (reduced || !definition.enabled) { m_from = m_to = new(); return new(); }
            return UiMotionSampling.Blend(m_from, m_to, UiMotionSampling.Progress(definition, Math.Max(0, time - m_start)));
        }
    }
    private readonly State m_hover = new(), m_focus = new(), m_pressed = new();
    private bool m_initialized, m_requested, m_waiting, m_wasShown;
    private double m_visibilityStart, m_waitingStart;
    private UiMotionPose m_visibilityFrom = new();
    internal bool visible { get; private set; }
    internal bool requested => m_requested;

    internal void ResolveVisibility(
        UiAnimationDefinition definition,
        bool requested,
        double time,
        bool reduced
    ) {
        if (!m_initialized || m_requested != requested)
        {
            m_visibilityFrom = requested && !visible && definition.enter.enabled ? definition.enter.pose.Copy() : m_initialized ? Visibility(definition, time, reduced) : new();
            m_visibilityStart = time; m_requested = requested; m_initialized = true;
        }
        m_wasShown |= requested;
        visible = requested || m_wasShown && !reduced && definition.exit.enabled && time - m_visibilityStart < definition.exit.delay + definition.exit.duration;
    }
    private UiMotionPose Visibility(
        UiAnimationDefinition definition,
        double time,
        bool reduced
    ) {
        var motion = m_requested ? definition.enter : definition.exit;
        if (reduced || !motion.enabled) return new();
        return UiMotionSampling.Blend(m_visibilityFrom, m_requested ? new() : motion.pose, UiMotionSampling.Progress(motion, Math.Max(0, time - m_visibilityStart)));
    }
    internal UiMotionPose Sample(
        UiAnimationDefinition definition,
        double time,
        bool hover,
        bool focus,
        bool pressed,
        bool waiting,
        bool reduced
    ) {
        var result = Visibility(definition, time, reduced);
        result = UiMotionSampling.Combine(result, m_hover.Sample(definition.hover, hover, time, reduced));
        result = UiMotionSampling.Combine(result, m_focus.Sample(definition.focus, focus, time, reduced));
        result = UiMotionSampling.Combine(result, m_pressed.Sample(definition.pressed, pressed, time, reduced));
        if (waiting != m_waiting) { m_waiting = waiting; m_waitingStart = time; }
        var loop = definition.waiting;
        double elapsed = time - m_waitingStart - loop.delay;
        if (waiting && loop.enabled && !reduced && elapsed >= 0)
        {
            double amount = loop.duration == 0 ? 1 : elapsed % (loop.duration * 2) / loop.duration;
            result = UiMotionSampling.Combine(result, UiMotionSampling.Sample(loop, loop.delay + (amount <= 1 ? amount : 2 - amount) * loop.duration));
        }
        return result;
    }
}

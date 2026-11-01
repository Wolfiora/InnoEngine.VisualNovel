using System;
using InnoEngine.Mathematics;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Text;
using Inno.VisualNovel.UI.Input;
using Inno.VisualNovel.UI.Text;

namespace Inno.VisualNovel.UI.Documents;

internal sealed class UiTooltipController
{
    private bool m_active, m_keyboard = true, m_pointerInside;
    private float m_x, m_y;
    private double m_elapsed;
    private string m_target = string.Empty, m_text = string.Empty;

    internal void Tick(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (m_active) m_elapsed = Math.Min(1, m_elapsed + seconds);
    }

    internal void Observe(UiInputSnapshot input)
    {
        if (input.pointerInside && (!m_pointerInside || input.x != m_x || input.y != m_y || input.pressed || input.scroll != 0)) m_keyboard = false;
        if (input.next || input.previous || input.increment || input.decrement || input.pageUp || input.pageDown || input.confirm || input.space) m_keyboard = true;
        m_pointerInside = input.pointerInside; m_x = input.x; m_y = input.y;
        m_active = !input.composing && !input.pressed && !input.held && !input.released && !input.confirm && !input.space && !input.cancel;
        if (!m_active) { m_elapsed = 0; m_target = m_text = string.Empty; }
    }

    internal UiDrawItem[] Build(
        UiElementInstance? hover,
        UiElementInstance? focus,
        float width,
        float height,
        Func<UiElementDefinition, string, (float width, float height)> measure,
        UiTextMeasurement? measureRich,
        UiRichTextResources resources,
        float scale
    ) {
        UiElementInstance? owner = m_keyboard ? focus : hover;
        if (!m_active || owner is not { inputVisible: true, behindStage: false } || string.IsNullOrWhiteSpace(owner.tooltip)
            || owner.visualOpacity <= .001f || !owner.visualBounds.Overlaps(owner.visualClip))
        { m_target = m_text = string.Empty; m_elapsed = 0; return []; }
        if (m_target != owner.id || m_text != owner.tooltip) { m_target = owner.id; m_text = owner.tooltip; m_elapsed = 0; }
        if (!m_keyboard && m_elapsed < .45) return [];
        UiStyleDefinition style = owner.definition.style;
        float margin = Math.Min(12, Math.Min(width, height) / 8), padding = Math.Min(Math.Max(8, style.textPadding), Math.Min(width, height) / 8);
        float maximumWidth = Math.Max(1, Math.Min(width - margin * 2, 720)), innerWidth = Math.Max(1, maximumWidth - padding * 2);
        var content = new UiRichText(NovelTextDocument.Literal(owner.tooltip));
        var definition = new UiElementDefinition { id = owner.id + "/tooltip", font = owner.definition.font, style = style.Copy() };
        (float width, float height) measured;
        if (measureRich is not null)
        {
            var result = measureRich(definition, content, resources, innerWidth, scale); measured = (result.width, result.height);
        }
        else
        {
            measured = measure(definition, owner.tooltip);
            measured.height *= Math.Max(1, (float)Math.Ceiling(measured.width / innerWidth));
        }
        if (!float.IsFinite(measured.width) || !float.IsFinite(measured.height) || measured.width < 0 || measured.height < 0)
            throw new InvalidOperationException("Tooltip text measurement must be finite and nonnegative: " + owner.id);
        float boxWidth = Math.Min(maximumWidth, Math.Max(style.fontSize + padding * 2, measured.width + padding * 2));
        float boxHeight = Math.Min(height - margin * 2, Math.Max(style.fontSize * style.lineSpacing, measured.height) + padding * 2);
        float x = Math.Clamp(owner.visualBounds.x, margin, Math.Max(margin, width - margin - boxWidth));
        float bottom = owner.visualBounds.y + owner.visualBounds.height + margin;
        float y = bottom + boxHeight <= height - margin ? bottom : Math.Max(margin, owner.visualBounds.y - margin - boxHeight);
        y = Math.Clamp(y, margin, Math.Max(margin, height - margin - boxHeight));
        var bounds = new Rect(x, y, boxWidth, boxHeight);
        return [new(definition.id, UiElementKind.Panel, bounds, new(0, 0, width, height), style.background | 255u, style.foreground | 255u,
            owner.tooltip, definition.font, style.fontSize, padding, null, false, style.focus, lineSpacing: style.lineSpacing,
            richText: content, textResources: resources, textScale: scale)];
    }

    internal void Cancel() { m_active = false; m_elapsed = 0; m_target = m_text = string.Empty; }
}

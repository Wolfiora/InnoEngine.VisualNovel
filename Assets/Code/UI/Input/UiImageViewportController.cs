using System;
using InnoEngine.Mathematics;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.UI.Layout;

namespace Inno.VisualNovel.UI.Input;

internal sealed class UiImageViewportController
{
    private object? m_source;
    private int m_width, m_height;
    private Rect m_viewport;
    private UiImageFit m_fit;
    private float m_zoom = 1;
    private Vector2 m_pan, m_last;
    private bool m_dragging;

    internal bool available => m_source is not null && m_width > 0 && m_height > 0 && m_viewport.width > 0 && m_viewport.height > 0;
    internal float zoom => m_zoom;
    internal Rect bounds
    {
        get
        {
            Rect fit = UiImageSizing.Fit(m_viewport, m_width, m_height, m_fit);
            float width = fit.width * m_zoom, height = fit.height * m_zoom;
            m_pan.x = Math.Clamp(m_pan.x, -Math.Max(0, (width - m_viewport.width) / 2), Math.Max(0, (width - m_viewport.width) / 2));
            m_pan.y = Math.Clamp(m_pan.y, -Math.Max(0, (height - m_viewport.height) / 2), Math.Max(0, (height - m_viewport.height) / 2));
            return new(m_viewport.x + (m_viewport.width - width) / 2 + m_pan.x, m_viewport.y + (m_viewport.height - height) / 2 + m_pan.y, width, height);
        }
    }

    internal void Synchronize(
        object? source,
        int width,
        int height,
        Rect viewport,
        UiImageFit fit
    ) {
        if (!ReferenceEquals(m_source, source) || m_width != width || m_height != height) Reset();
        m_source = source; m_width = width; m_height = height; m_viewport = viewport; m_fit = fit;
    }

    internal void Reset() { m_zoom = 1; m_pan = default; m_dragging = false; }

    internal void Zoom(
        float steps,
        float x,
        float y
    ) {
        if (!available) return;
        if (!float.IsFinite(steps) || !float.IsFinite(x) || !float.IsFinite(y)) throw new ArgumentOutOfRangeException(nameof(steps));
        Rect previous = bounds;
        float next = Math.Clamp(m_zoom * MathF.Pow(1.25f, Math.Clamp(steps, -32, 32)), 1, 16);
        float ratio = next / m_zoom; m_zoom = next;
        m_pan = new(x - (x - previous.x) * ratio - m_viewport.x - m_viewport.width / 2 + previous.width * ratio / 2,
            y - (y - previous.y) * ratio - m_viewport.y - m_viewport.height / 2 + previous.height * ratio / 2);
        _ = bounds;
    }

    internal void BeginDrag(
        float x,
        float y
    ) { m_last = new(x, y); m_dragging = available && float.IsFinite(x) && float.IsFinite(y); }

    internal void Drag(
        float x,
        float y
    ) {
        if (!m_dragging || !float.IsFinite(x) || !float.IsFinite(y)) return;
        Pan(x - m_last.x, y - m_last.y); m_last = new(x, y);
    }

    internal void Pan(
        float x,
        float y
    ) { if (!available) return; m_pan.x += x; m_pan.y += y; _ = bounds; }

    internal void EndDrag() => m_dragging = false;
}

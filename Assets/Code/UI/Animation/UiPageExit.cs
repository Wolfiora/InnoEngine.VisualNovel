using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Mathematics;
using Inno.VisualNovel.Model.Interface.Animation;
using Inno.VisualNovel.UI.Documents;
using Inno.VisualNovel.UI.Layout;

namespace Inno.VisualNovel.UI.Animation;

/// <summary>Owns a frozen, input-free outgoing page; no bindings, actions, or story state are retained.</summary>
public sealed class UiPageExit
{
    internal sealed record Element(UiDrawItem draw, string parent, bool clipsChildren, Vector2 pivot, UiMotionDefinition motion);
    private readonly Element[] m_elements;
    private readonly float m_width, m_height;
    private readonly double m_duration;
    private double m_time;
    internal UiPageExit(
        float width,
        float height,
        IEnumerable<Element> elements
    ) {
        m_width = width; m_height = height; m_elements = elements.ToArray();
        m_duration = m_elements.Where(static item => item.motion.enabled).Select(static item => item.motion.delay + item.motion.duration).DefaultIfEmpty(0).Max();
    }
    /// <summary>Gets whether the frozen page has completely retired.</summary>
    public bool complete => m_time >= m_duration;
    /// <summary>Advances only outgoing presentation time.</summary>
    /// <param name="seconds">A finite nonnegative logical interval.</param>
    public void Tick(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        m_time = Math.Min(m_duration, m_time + seconds);
    }
    /// <summary>Samples the immutable outgoing image without consulting its former page data.</summary>
    /// <returns>An empty image after completion, otherwise detached presentation commands.</returns>
    public UiFrame BuildFrame()
    {
        if (complete) return new(m_width, m_height, []);
        var result = new List<UiDrawItem>();
        var parents = new Dictionary<string, (Matrix extra, Rect childClip, float opacity)>(StringComparer.Ordinal);
        foreach (var item in m_elements)
        {
            var parent = parents.TryGetValue(item.parent, out var found) ? found : (Matrix.identity, new Rect(0, 0, m_width, m_height), 1f);
            UiMotionPose pose = UiMotionSampling.Sample(item.motion, m_time);
            Rect previousBounds = UiMotionSampling.Bounds(item.draw.bounds, item.draw.presentation ?? Matrix.identity);
            Matrix extra = parent.Item1 * UiMotionSampling.Transform(previousBounds, item.pivot, pose);
            Matrix presentation = extra * (item.draw.presentation ?? Matrix.identity);
            Rect bounds = UiMotionSampling.Bounds(item.draw.bounds, presentation);
            Rect.TryIntersect(bounds, parent.Item2, out Rect clip);
            float opacity = pose.opacity * parent.Item3;
            result.Add(item.draw with { presentation = presentation, clip = clip, opacity = item.draw.opacity * opacity, focused = false });
            parents.Add(item.draw.id, (extra, item.clipsChildren ? clip : parent.Item2, opacity));
        }
        return new(m_width, m_height, result.AsReadOnly());
    }
    /// <summary>Paints the outgoing image below a new input owner's canvas using the shared aspect fit.</summary>
    /// <param name="incoming">The new page's detached image.</param>
    /// <returns>A common-canvas frame; outgoing identities are explicitly presentation-only.</returns>
    public UiFrame Composite(UiFrame incoming)
    {
        UiFrame previous = BuildFrame();
        UiCanvasFit fit = UiCanvasFit.Create(previous.width, previous.height, new(0, 0, incoming.width, incoming.height));
        Matrix projection = Matrix.CreateTranslation(fit.bounds.x, fit.bounds.y, 0) * Matrix.CreateScale(fit.scaleX, fit.scaleY, 1);
        return new(incoming.width, incoming.height, Array.AsReadOnly(previous.items.Select(item => item with
        { id = "outgoing/" + item.id, presentation = projection * (item.presentation ?? Matrix.identity), clip = fit.Project(item.clip) }).Concat(incoming.items).ToArray()));
    }
}

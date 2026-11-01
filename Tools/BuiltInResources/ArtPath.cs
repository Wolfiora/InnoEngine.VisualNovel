using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal sealed class ArtPath : IDisposable
{
    private readonly StringBuilder m_svg = new();
    private PointF m_point;
    internal GraphicsPath native { get; } = new();
    internal string svg => m_svg.ToString();
    internal ArtPath M(
        float x,
        float y
    ) { native.StartFigure(); m_point = new(x, y); Command("M", x, y); return this; }
    internal ArtPath L(
        float x,
        float y
    ) { native.AddLine(m_point, new(x, y)); m_point = new(x, y); Command("L", x, y); return this; }
    internal ArtPath C(
        float x1,
        float y1,
        float x2,
        float y2,
        float x,
        float y
    ) { native.AddBezier(m_point, new(x1, y1), new(x2, y2), new(x, y)); m_point = new(x, y); Command("C", x1, y1, x2, y2, x, y); return this; }
    internal ArtPath Z() { native.CloseFigure(); m_svg.Append('Z'); return this; }
    private void Command(
        string command,
        params float[] values
    ) {
        m_svg.Append(command);
        foreach (float value in values) m_svg.Append(value.ToString("0.###", CultureInfo.InvariantCulture)).Append(' ');
    }
    public void Dispose() => native.Dispose();
}

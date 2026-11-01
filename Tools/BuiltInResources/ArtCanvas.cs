using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal sealed class ArtCanvas : IDisposable
{
    private readonly Bitmap m_bitmap;
    private readonly Graphics m_graphics;
    private readonly StringBuilder m_svg = new();
    private int m_gradient;
    private int m_groups;
    internal ArtCanvas(
        int width,
        int height,
        float designWidth = 0,
        float designHeight = 0
    ) {
        m_bitmap = new(width, height, PixelFormat.Format32bppArgb); m_graphics = Graphics.FromImage(m_bitmap);
        m_graphics.Clear(Color.Transparent); m_graphics.SmoothingMode = SmoothingMode.AntiAlias; m_graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        float canvasWidth = designWidth > 0 ? designWidth : width, canvasHeight = designHeight > 0 ? designHeight : height;
        m_graphics.ScaleTransform(width / canvasWidth, height / canvasHeight);
        m_svg.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {canvasWidth} {canvasHeight}\">\n");
    }
    internal void Draw(
        ArtPath path,
        uint? fill,
        uint? stroke = null,
        float width = 2
    ) {
        if (fill is { } color) { using var brush = new SolidBrush(Rgba(color)); m_graphics.FillPath(brush, path.native); }
        if (stroke is { } outline)
        { using var pen = new Pen(Rgba(outline), width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }; m_graphics.DrawPath(pen, path.native); }
        m_svg.Append(CultureInfo.InvariantCulture, $"<path d=\"{path.svg}\" fill=\"{Css(fill)}\" stroke=\"{Css(stroke)}\" stroke-width=\"{width}\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>\n");
    }
    internal void Translate(
        float x,
        float y
    ) {
        m_graphics.TranslateTransform(x, y);
        m_svg.Append(CultureInfo.InvariantCulture, $"<g transform=\"translate({x} {y})\">\n"); m_groups++;
    }
    internal void Rect(
        uint color,
        float x,
        float y,
        float width,
        float height
    ) {
        using var path = new ArtPath(); path.M(x, y).L(x + width, y).L(x + width, y + height).L(x, y + height).Z(); Draw(path, color);
    }
    internal void Ellipse(
        uint color,
        float x,
        float y,
        float width,
        float height
    ) {
        using var brush = new SolidBrush(Rgba(color)); m_graphics.FillEllipse(brush, x, y, width, height);
        m_svg.Append(CultureInfo.InvariantCulture, $"<ellipse cx=\"{x + width / 2}\" cy=\"{y + height / 2}\" rx=\"{width / 2}\" ry=\"{height / 2}\" fill=\"{Css(color)}\"/>\n");
    }
    internal void Gradient(
        uint top,
        uint bottom,
        float x,
        float y,
        float width,
        float height
    ) {
        using var brush = new LinearGradientBrush(new RectangleF(x, y, width, height), Rgba(top), Rgba(bottom), 90);
        m_graphics.FillRectangle(brush, x, y, width, height);
        int id = m_gradient++;
        m_svg.Append(CultureInfo.InvariantCulture, $"<defs><linearGradient id=\"g{id}\" x2=\"0\" y2=\"1\"><stop stop-color=\"{Css(top)}\"/><stop offset=\"1\" stop-color=\"{Css(bottom)}\"/></linearGradient></defs><rect x=\"{x}\" y=\"{y}\" width=\"{width}\" height=\"{height}\" fill=\"url(#g{id})\"/>\n");
    }
    internal void Save(
        string root,
        string relative
    ) {
        string assetRoot = Path.GetFullPath(Path.Combine(root, "Assets")) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(assetRoot, relative));
        if (!path.StartsWith(assetRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Artwork escaped its explicit repository asset boundary.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (!File.Exists(path)) m_bitmap.Save(path, ImageFormat.Png);
        string vector = Path.Combine(root, "Tools", "ArtResources", "Sources", Path.ChangeExtension(relative, ".svg"));
        Directory.CreateDirectory(Path.GetDirectoryName(vector)!);
        if (!File.Exists(vector)) File.WriteAllText(vector, m_svg + string.Concat(System.Linq.Enumerable.Repeat("</g>", m_groups)) + "</svg>\n", new UTF8Encoding(false));
    }
    private static Color Rgba(uint value) => Color.FromArgb((int)(value & 255), (int)(value >> 24), (int)(value >> 16 & 255), (int)(value >> 8 & 255));
    private static string Css(uint? value) => value is not { } color ? "none" : "#" + (color >> 8).ToString("X6", CultureInfo.InvariantCulture) + (color & 255).ToString("X2", CultureInfo.InvariantCulture);
    public void Dispose() { m_graphics.Dispose(); m_bitmap.Dispose(); }
}

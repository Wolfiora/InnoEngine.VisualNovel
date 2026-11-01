using InnoEngine.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Rendering;
using InnoEngine.Text;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.UI.Documents;
using Inno.VisualNovel.Stage.Composition;
using Inno.VisualNovel.UI.Layout;
using Inno.VisualNovel.UI.Text;
using Inno.VisualNovel.UI.Animation;
using Inno.VisualNovel.Model.Text;
using NumericsVector2 = System.Numerics.Vector2;

namespace Inno.VisualNovel.Rendering.Frames;

/// <summary>Converts detached UI frames to quads using the engine's Unicode shaping and rasterization.</summary>
public sealed class NovelFrameComposer
{
    private readonly Dictionary<string, GlyphBitmap> m_glyphs = new(StringComparer.Ordinal);

    /// <summary>Builds one frame without owning a graphics device.</summary>
    /// <param name="frame">The fully resolved UI frame.</param>
    /// <param name="material">The native alpha material.</param>
    /// <param name="stage">Optional stage extraction, painted before game UI.</param>
    /// <param name="targetWidth">Optional final design width, independent of the authored page.</param>
    /// <param name="targetHeight">Optional final design height, independent of the authored page.</param>
    /// <returns>Immutable draw commands in authored order.</returns>
    public NovelRenderFrame Compose(
        UiFrame frame,
        MaterialAsset material,
        StageFrame? stage = null,
        float? targetWidth = null,
        float? targetHeight = null,
        MaterialAsset? effectsMaterial = null,
        UiFrame? previousUi = null
    ) {
        float width = targetWidth ?? frame.width, height = targetHeight ?? frame.height;
        var underlay = PaintUi(frame, width, height, true); var overlay = PaintUi(frame, width, height, false);
        var stageQuads = PaintStage(stage, width, height);
        var quads = underlay.Concat(stageQuads).Concat(overlay).ToArray();
        bool processing = NovelStageImagePlanner.RequiresProcessing(stage) || stage?.transition is not null || stage?.effects?.Count > 0;
        if (processing && effectsMaterial is null) throw new InvalidOperationException("Screen effects require the game's native image-composition material.");
        return new(width, height, material, Array.AsReadOnly(quads))
        {
            effectsMaterial = effectsMaterial,
            image = processing ? new NovelImagePlanner(PaintUi, PaintStage).Build(stage!, frame, previousUi, width, height) : null
        };
    }

    private static IReadOnlyList<NovelDrawQuad> PaintStage(
        StageFrame? stage,
        float width,
        float height
    ) => stage?.items.Select(item => new NovelDrawQuad(new(0, 0, width, height), new(0, 0, width, height), item.color,
        item.texture is null ? new("white") : new("image/" + item.texture.identity.persistentId, item.texture),
        new(item.topLeft, item.topRight, item.bottomRight, item.bottomLeft), item.uv)).ToArray() ?? [];

    private IReadOnlyList<NovelDrawQuad> PaintUi(
        UiFrame frame,
        float width,
        float height,
        bool behindStage
    ) {
        var quads = new List<NovelDrawQuad>();
        UiCanvasFit fit = UiCanvasFit.Create(frame.width, frame.height, new(0, 0, width, height));
        var white = new NovelTextureSource("white");
        foreach (UiDrawItem item in frame.items.Where(item => item.behindStage == behindStage))
        {
            if (item.clip.width <= 0 || item.clip.height <= 0) continue;
            int firstQuad = quads.Count;
            if ((item.background & 255) > 0) quads.Add(new(item.bounds, item.clip, item.background, white));
            if (item.image is not null) AddImage(new("image/" + item.image.identity.persistentId, item.image), item.image.width, item.image.height);
            else if (item.bitmap is { } bitmap) AddImage(new("bitmap/" + bitmap.contentKey, bitmap: bitmap), bitmap.width, bitmap.height);
            if (item.focused)
            {
                Rect b = item.bounds;
                quads.Add(new(new(b.x, b.y, b.width, 2), item.clip, item.focusColor, white));
                quads.Add(new(new(b.x, b.y + b.height - 2, b.width, 2), item.clip, item.focusColor, white));
                quads.Add(new(new(b.x, b.y, 2, b.height), item.clip, item.focusColor, white));
                quads.Add(new(new(b.x + b.width - 2, b.y, 2, b.height), item.clip, item.focusColor, white));
            }
            if (item.kind == UiElementKind.Toggle)
            {
                bool flag = item.text == "True" || item.text == "true";
                quads.Add(new(new(item.bounds.x + 8, item.bounds.y + 8, 24, 24), item.clip, flag ? item.focusColor : 0x555C70FF, white));
            }
            if (item.kind is UiElementKind.Slider or UiElementKind.Progress)
            {
                Rect b = item.bounds;
                float trackWidth = Math.Max(0, b.width - 32), trackY = b.y + b.height - 12;
                quads.Add(new(new(b.x + 16, trackY, trackWidth, 4), item.clip, 0x545C70FF, white));
                quads.Add(new(new(b.x + 16, trackY, trackWidth * (float)item.progress, 4), item.clip, item.focusColor, white));
                if (item.kind == UiElementKind.Slider) quads.Add(new(new(b.x + 10 + trackWidth * (float)item.progress, trackY - 4, 12, 12), item.clip, item.foreground, white));
            }
            if (item.inputPresentation?.selection is { } selection)
                quads.Add(new(selection, item.clip, item.focusColor & 0xFFFFFF00 | (uint)Math.Round((item.foreground & 255) * .3), white));
            if ((item.richText?.document.text.Length > 0 || item.text.Length > 0) && item.font is not null) AddText(item, quads);
            if (item.inputPresentation?.caret is { } caret) quads.Add(new(caret, item.clip, item.foreground, white));
            for (int index = firstQuad; index < quads.Count; index++)
            {
                NovelDrawQuad quad = quads[index];
                Matrix matrix = item.presentation ?? Matrix.identity;
                NovelQuadCorners? corners = quad.corners is { } source ? new(Project(source.topLeft), Project(source.topRight), Project(source.bottomRight), Project(source.bottomLeft)) : null;
                quads[index] = quad with { bounds = UiMotionSampling.Bounds(quad.bounds, matrix), corners = corners,
                    color = quad.color & 0xFFFFFF00 | (uint)Math.Round((quad.color & 255) * item.opacity) };
                NumericsVector2 Project(NumericsVector2 point)
                {
                    Vector2 result = Vector2.Transform(new(point.X, point.Y), matrix); return new(result.x, result.y);
                }
            }

            void AddImage(
                NovelTextureSource texture,
                int sourceWidth,
                int sourceHeight
            ) {
                if (item.imageBounds is { } imageBounds) quads.Add(new(imageBounds, item.clip, item.imageTint, texture));
                else if (item.imageFit == UiImageFit.NineSlice)
                    quads.AddRange(Geometry.NineSliceBuilder.Build(texture, sourceWidth, sourceHeight, item.bounds, item.clip, item.imageTint, item.imageSlice ?? new()));
                else quads.Add(new(UiImageSizing.Fit(item.bounds, sourceWidth, sourceHeight, item.imageFit), item.clip, item.imageTint, texture));
            }
        }
        for (int index = 0; index < quads.Count; index++)
        {
            NovelDrawQuad source = quads[index];
            NovelQuadCorners? points = source.corners is { } corners ? new(fit.Project(corners.topLeft), fit.Project(corners.topRight), fit.Project(corners.bottomRight), fit.Project(corners.bottomLeft)) : null;
            quads[index] = source with { bounds = fit.Project(source.bounds), clip = fit.Project(source.clip), corners = points };
        }
        return quads.AsReadOnly();
    }

    /// <summary>Measures authored text with the same engine shaper used for drawing.</summary>
    /// <param name="element">The current definition.</param>
    /// <param name="text">The resolved text.</param>
    /// <returns>Logical size before padding.</returns>
    public static (float width, float height) Measure(
        UiElementDefinition element,
        string text
    ) {
        if (element.font is null) return (0, element.style.fontSize * element.style.lineSpacing);
        NovelTextLayout layout = new NovelTextLayoutBuilder().Build(new(element.richText ? NovelTextMarkup.Parse(text) : NovelTextDocument.Literal(text)), element.font,
            element.style.fontSize, Math.Max(0, element.layout.width - element.style.textPadding * 2), element.style.lineSpacing, element.style.alignment);
        return (layout.width, layout.height);
    }

    /// <summary>Measures full rich text with the exact width-aware layout used by painting.</summary>
    /// <param name="element">The current declaration with inherited font.</param>
    /// <param name="content">Full content independent of its reveal boundary.</param>
    /// <param name="resources">Page-owned fonts and inline images.</param>
    /// <param name="width">Available inner text width.</param>
    /// <param name="scale">Accessibility scale for absolute scope sizes.</param>
    /// <returns>Stable full-text logical bounds.</returns>
    public static (float width, float height, float visibleHeight) MeasureRichText(
        UiElementDefinition element,
        UiRichText content,
        UiRichTextResources resources,
        float width,
        float scale
    ) {
        if (element.font is null) return (0, element.style.fontSize * element.style.lineSpacing, element.style.fontSize * element.style.lineSpacing);
        NovelTextLayout layout = new NovelTextLayoutBuilder().Build(content, element.font, element.style.fontSize, width,
            element.style.lineSpacing, element.style.alignment, resources, scale);
        return (layout.width, layout.height, layout.visibleHeight);
    }

    /// <summary>Releases generation-local CPU glyph data.</summary>
    public void Clear() => m_glyphs.Clear();

    private void AddText(
        UiDrawItem item,
        List<NovelDrawQuad> quads
    ) {
        NovelTextLayout layout = new NovelTextLayoutBuilder().Build(item.richText ?? new(NovelTextDocument.Literal(item.text)), item.font!, item.fontSize,
            Math.Max(0, item.bounds.width - item.textPadding * 2), item.lineSpacing, item.alignment, item.textResources, item.textScale);
        float left = item.bounds.x + item.textPadding, top = item.bounds.y + item.textPadding;
        foreach (NovelTextImage image in layout.images)
            quads.Add(new(new(left + image.bounds.x, top + image.bounds.y, image.bounds.width, image.bounds.height), item.clip, 0xFFFFFFFF,
                new("image/" + image.image.identity.persistentId, image.image)));
        foreach (NovelTextGlyph positioned in layout.glyphs)
        {
            TextGlyph glyph = positioned.glyph; FontAsset font = positioned.font; NovelTextStyle style = positioned.style;
            string key = $"font/{font.identity.runtimeIdentity}/{font.identity.persistentId}/{positioned.size:R}/{glyph.glyphId}";
            if (!m_glyphs.TryGetValue(key, out GlyphBitmap? bitmap))
            {
                bitmap = InnoEngine.Text.Text.Rasterize(font, 0, glyph.glyphId, positioned.size);
                if (m_glyphs.Count >= 4096) m_glyphs.Clear(); m_glyphs[key] = bitmap;
            }
            if (bitmap.width == 0 || bitmap.height == 0) continue;
            if (style.shadow != 0) Paint(bitmap, key, TintAlpha(style.shadowColor), style.shadow * item.textScale, style.shadow * item.textScale);
            if (style.outline > 0)
            {
                int radius = Math.Clamp((int)Math.Ceiling(style.outline * item.textScale), 1, 32); string outlineKey = key + "/outline/" + radius;
                if (!m_glyphs.TryGetValue(outlineKey, out GlyphBitmap? outline))
                {
                    outline = Inno.VisualNovel.Rendering.Text.GlyphOutline.Build(bitmap, radius);
                    if (m_glyphs.Count >= 4096) m_glyphs.Clear(); m_glyphs[outlineKey] = outline;
                }
                Paint(outline, outlineKey, TintAlpha(style.outlineColor), 0, 0);
            }
            uint foreground = style.color is { } color ? TintAlpha(color) : item.foreground;
            Paint(bitmap, key, foreground, 0, 0);
            if (style.bold) Paint(bitmap, key, foreground, Math.Max(0.5f, positioned.size / 32), 0);

            uint TintAlpha(uint color) => color & 0xFFFFFF00 | (color & 255) * (item.foreground & 255) / 255;
            void Paint(GlyphBitmap coverage, string textureKey, uint color, float offsetX, float offsetY)
            {
                Rect bounds = new(left + positioned.x + glyph.offsetX + coverage.bearingX + offsetX,
                    top + positioned.baseline - glyph.offsetY - coverage.bearingY + offsetY, coverage.width, coverage.height);
                NovelQuadCorners? corners = null;
                if (style.italic)
                {
                    float topSkew = (positioned.baseline + top - bounds.y) * 0.2f;
                    float bottomSkew = (positioned.baseline + top - bounds.y - bounds.height) * 0.2f;
                    corners = new(new NumericsVector2(bounds.x + topSkew, bounds.y), new NumericsVector2(bounds.x + bounds.width + topSkew, bounds.y),
                        new NumericsVector2(bounds.x + bounds.width + bottomSkew, bounds.y + bounds.height), new NumericsVector2(bounds.x + bottomSkew, bounds.y + bounds.height));
                }
                quads.Add(new(bounds, item.clip, color, new(textureKey, glyph: coverage), corners));
            }
        }
    }
}

using InnoEngine.Mathematics;
using System.Collections.Generic;
using InnoEngine.Text;
using InnoEngine.Rendering;
using Inno.VisualNovel.Model.Interface;

namespace Inno.VisualNovel.UI.Documents;

/// <summary>Describes one immutable paint command in back-to-front order.</summary>
/// <param name="id">The element identity.</param>
/// <param name="kind">The control kind.</param>
/// <param name="bounds">Resolved logical bounds.</param>
/// <param name="clip">Effective ancestor clip.</param>
/// <param name="background">Packed RRGGBBAA fill.</param>
/// <param name="foreground">Packed RRGGBBAA text.</param>
/// <param name="text">Resolved display text.</param>
/// <param name="font">The selected imported font.</param>
/// <param name="fontSize">Logical type size.</param>
/// <param name="textPadding">Inset used by text.</param>
/// <param name="image">Optional imported image.</param>
/// <param name="focused">Whether to draw the focus outline.</param>
/// <param name="focusColor">Packed outline color.</param>
/// <param name="progress">Normalized slider or progress-bar value.</param>
/// <param name="lineSpacing">Authored line-height multiplier.</param>
/// <param name="imageFit">Aspect-preserving image sizing.</param>
/// <param name="richText">The full shaped text and independent visible range.</param>
/// <param name="textResources">Borrowed page-owned alternate fonts and inline images.</param>
/// <param name="alignment">Wrapped-line alignment.</param>
/// <param name="textScale">The accessibility scale applied to absolute sizes in text scopes.</param>
/// <param name="behindStage">Whether this decorative item belongs below stage composition.</param>
/// <param name="bitmap">Optional immutable generated image independent of imported assets.</param>
/// <param name="imageTint">Packed image tint including complete element opacity.</param>
/// <param name="imageSlice">Detached nine-slice source borders.</param>
/// <param name="imageBounds">Optional resolved image geometry inside the unchanged clipped control bounds.</param>
/// <param name="presentation">Optional engine transform applied after stable text and image layout.</param>
/// <param name="opacity">Additional complete animation opacity, including ancestors.</param>
/// <param name="inputPresentation">Transient text selection and caret geometry, absent from noninteractive capture.</param>
public sealed record UiDrawItem(
    string id,
    UiElementKind kind,
    Rect bounds,
    Rect clip,
    uint background,
    uint foreground,
    string text,
    FontAsset? font,
    float fontSize,
    float textPadding,
    TextureAsset? image,
    bool focused,
    uint focusColor,
    double progress = 0,
    float lineSpacing = 1.35f,
    UiImageFit imageFit = UiImageFit.Stretch,
    Inno.VisualNovel.UI.Text.UiRichText? richText = null,
    Inno.VisualNovel.UI.Text.UiRichTextResources? textResources = null,
    UiTextAlignment alignment = UiTextAlignment.Left,
    float textScale = 1,
    bool behindStage = false,
    Imaging.UiBitmap? bitmap = null,
    uint imageTint = 0xFFFFFFFF,
    UiImageSlice? imageSlice = null,
    Rect? imageBounds = null,
    Matrix? presentation = null,
    float opacity = 1,
    UiTextInputPresentation? inputPresentation = null
);

using System;
using System.Linq;
using InnoEngine.Mathematics;
using InnoEngine.Serialization;
using InnoEngine.Rendering;
using InnoEngine.Text;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Text;
using Inno.VisualNovel.UI.Text;

namespace Inno.VisualNovel.UI.Documents;

/// <summary>Stores one paint command independently of its source element and asset object instances.</summary>
[GenerateSerializationConverter]
public sealed class UiPaintSnapshot : ISerializable
{
    /// <summary>Gets or sets the element identity and persistent resource references.</summary>
    [SerializableProperty] public string[] keys { get; set; } = ["", "", ""];
    /// <summary>Gets or sets bounds, clip, type size, padding, progress, line spacing, text scale and optional image geometry.</summary>
    [SerializableProperty] public double[] geometry { get; set; } = [];
    /// <summary>Gets or sets fill, text and focus colors.</summary>
    [SerializableProperty] public uint[] colors { get; set; } = [];
    /// <summary>Gets or sets control kind, sizing, alignment and visible grapheme count.</summary>
    [SerializableProperty] public int[] modes { get; set; } = [];
    /// <summary>Gets or sets focus, rich-text, stage-underlay and explicit-image-geometry flags.</summary>
    [SerializableProperty] public bool[] flags { get; set; } = [];
    /// <summary>Gets or sets literal or resolved, escaped rich markup.</summary>
    [SerializableProperty] public string text { get; set; } = "";
    /// <summary>Gets or sets alternating rich-text font keys and persistent identities.</summary>
    [SerializableProperty] public string[] fonts { get; set; } = [];
    /// <summary>Gets or sets alternating inline-image keys and persistent identities.</summary>
    [SerializableProperty] public string[] images { get; set; } = [];
    /// <summary>Gets or sets ordered persistent font identities used by native fallback shaping.</summary>
    [SerializableProperty] public string[] fallbackFonts { get; set; } = [];
    /// <summary>Gets or sets a standard PNG for generated imagery, empty for imported images.</summary>
    [SerializableProperty] public byte[] bitmap { get; set; } = [];
    /// <summary>Gets or sets detached source-pixel borders for the captured image.</summary>
    [SerializableProperty] public UiImageSlice imageSlice { get; set; } = new();
    /// <summary>Gets or sets the captured engine presentation transform after stable text layout.</summary>
    [SerializableProperty] public Matrix presentation { get; set; } = Matrix.identity;
    /// <summary>Gets or sets additional complete presentation opacity.</summary>
    [SerializableProperty] public float opacity { get; set; } = 1;

    internal static UiPaintSnapshot Capture(UiDrawItem item) => new()
    {
        keys = [item.id, item.font?.identity.persistentId.ToString("D") ?? "", item.image?.identity.persistentId.ToString("D") ?? ""],
        geometry = [item.bounds.x, item.bounds.y, item.bounds.width, item.bounds.height, item.clip.x, item.clip.y, item.clip.width, item.clip.height,
            item.fontSize, item.textPadding, item.progress, item.lineSpacing, item.textScale,
            item.imageBounds?.x ?? 0, item.imageBounds?.y ?? 0, item.imageBounds?.width ?? 0, item.imageBounds?.height ?? 0],
        colors = [item.background, item.foreground, item.focusColor, item.imageTint], modes = [(int)item.kind, (int)item.imageFit, (int)item.alignment, item.richText?.visibleGraphemes ?? int.MaxValue],
        flags = [item.focused, item.richText is not null, item.behindStage, item.imageBounds is not null], text = item.richText?.document.markup ?? item.text,
        fonts = item.textResources?.fontBindings.SelectMany(static item => new[] { item.key, item.font?.identity.persistentId.ToString("D") ?? "" }).ToArray() ?? [],
        images = item.textResources?.iconBindings.SelectMany(static item => new[] { item.key, item.image?.identity.persistentId.ToString("D") ?? "" }).ToArray() ?? [],
        fallbackFonts = item.textResources?.fallbackFonts.Select(static font => font.identity.persistentId.ToString("D")).ToArray() ?? [],
        bitmap = item.bitmap is null ? [] : Imaging.UiPngImage.Encode(item.bitmap), imageSlice = item.imageSlice?.Copy() ?? new(),
        presentation = item.presentation ?? Matrix.identity, opacity = item.opacity
    };

    internal UiDrawItem Restore(
        Func<string, FontAsset> font,
        Func<string, TextureAsset> image
    ) {
        if (!float.IsFinite(opacity) || opacity is < 0 or > 1 || !float.IsFinite(presentation.m11) || !float.IsFinite(presentation.m22)
            || presentation.m11 <= 0 || presentation.m22 <= 0
            || presentation != Matrix.CreateTranslation(presentation.m14, presentation.m24, 0) * Matrix.CreateScale(presentation.m11, presentation.m22, 1))
            throw new InvalidOperationException("Captured UI presentation requires finite positive scale, translation and opacity.");
        if (keys.Length != 3 || keys.Any(static key => key.Length > 1024) || geometry.Length != 17 || geometry.Any(static value => !double.IsFinite(value) || Math.Abs(value) > 1e8)
            || colors.Length != 4 || modes.Length != 4 || flags.Length != 4 || text.Length > 1000000 || fonts.Length > 256 || images.Length > 512 || fallbackFonts.Length > 32
            || fallbackFonts.Distinct(StringComparer.Ordinal).Count() != fallbackFonts.Length
            || fonts.Length % 2 != 0 || images.Length % 2 != 0 || modes[3] < 0 || !Enum.IsDefined((UiElementKind)modes[0])
            || !Enum.IsDefined((UiImageFit)modes[1]) || !Enum.IsDefined((UiTextAlignment)modes[2]) || geometry[2] < 0 || geometry[3] < 0 || geometry[6] < 0 || geometry[7] < 0
            || geometry[8] <= 0 || geometry[9] < 0 || geometry[10] is < 0 or > 1 || geometry[11] <= 0 || geometry[12] <= 0
            || flags[3] && (geometry[15] <= 0 || geometry[16] <= 0 || (UiImageFit)modes[1] == UiImageFit.NineSlice))
            throw new InvalidOperationException("Captured UI paint contains invalid geometry, modes or text resources.");
        var resources = new UiRichTextResources(Enumerable.Range(0, fonts.Length / 2).Select(index => new UiTextFont { key = fonts[index * 2], font = fonts[index * 2 + 1].Length == 0 ? null : font(fonts[index * 2 + 1]) }),
            Enumerable.Range(0, images.Length / 2).Select(index => new UiTextIcon { key = images[index * 2], image = images[index * 2 + 1].Length == 0 ? null : image(images[index * 2 + 1]) }), fallbackFonts.Select(font));
        UiRichText? rich = flags[1] ? new(NovelTextMarkup.Parse(text), modes[3]) : null;
        TextureAsset? texture = keys[2].Length == 0 ? null : image(keys[2]);
        Imaging.UiBitmap? generated = bitmap.Length == 0 ? null : Imaging.UiPngImage.Decode(bitmap);
        imageSlice.Validate((UiImageFit)modes[1] == UiImageFit.NineSlice ? texture?.width ?? generated?.width ?? 0 : 0,
            (UiImageFit)modes[1] == UiImageFit.NineSlice ? texture?.height ?? generated?.height ?? 0 : 0);
        return new(keys[0], (UiElementKind)modes[0], new((float)geometry[0], (float)geometry[1], (float)geometry[2], (float)geometry[3]),
            new((float)geometry[4], (float)geometry[5], (float)geometry[6], (float)geometry[7]), colors[0], colors[1], rich?.document.text ?? text,
            keys[1].Length == 0 ? null : font(keys[1]), (float)geometry[8], (float)geometry[9], texture, flags[0], colors[2],
            geometry[10], (float)geometry[11], (UiImageFit)modes[1], rich, resources, (UiTextAlignment)modes[2], (float)geometry[12], flags[2], generated, colors[3], imageSlice.Copy(),
            flags[3] ? new Rect((float)geometry[13], (float)geometry[14], (float)geometry[15], (float)geometry[16]) : null, presentation, opacity);
    }
}

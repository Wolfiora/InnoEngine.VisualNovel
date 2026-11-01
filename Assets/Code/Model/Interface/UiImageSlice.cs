using System;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Interface;

/// <summary>Declares source-pixel borders preserved by nine-slice image composition.</summary>
[GenerateSerializationConverter]
public sealed class UiImageSlice : ISerializable
{
    /// <summary>Gets or sets the source left border in pixels.</summary>
    [SerializableProperty] public float left { get; set; }
    /// <summary>Gets or sets the source top border in pixels.</summary>
    [SerializableProperty] public float top { get; set; }
    /// <summary>Gets or sets the source right border in pixels.</summary>
    [SerializableProperty] public float right { get; set; }
    /// <summary>Gets or sets the source bottom border in pixels.</summary>
    [SerializableProperty] public float bottom { get; set; }
    /// <summary>Gets or sets design pixels per preserved source-border pixel.</summary>
    [SerializableProperty] public float scale { get; set; } = 1;
    /// <summary>Gets or sets whether the stretched middle region is painted.</summary>
    [SerializableProperty] public bool drawCenter { get; set; } = true;
    /// <summary>Creates an independent border declaration for a detached frame or edit transaction.</summary>
    /// <returns>A mutable copy independent of the source.</returns>
    public UiImageSlice Copy() => new() { left = left, top = top, right = right, bottom = bottom, scale = scale, drawCenter = drawCenter };
    /// <summary>Checks finite nonnegative borders, scale and optional source-image bounds.</summary>
    /// <param name="width">Source width, or zero when a dynamic image is not resolved yet.</param>
    /// <param name="height">Source height, or zero when a dynamic image is not resolved yet.</param>
    public void Validate(
        int width = 0,
        int height = 0
    ) {
        if (!float.IsFinite(left) || !float.IsFinite(top) || !float.IsFinite(right) || !float.IsFinite(bottom) || !float.IsFinite(scale)
            || left < 0 || top < 0 || right < 0 || bottom < 0 || left > 16384 || top > 16384 || right > 16384 || bottom > 16384
            || scale <= 0 || scale > 64 || width > 0 && left + right > width || height > 0 && top + bottom > height)
            throw new InvalidOperationException("Nine-slice borders require finite nonnegative pixels fitting their source image and a positive bounded scale.");
    }
}

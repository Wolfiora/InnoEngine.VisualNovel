using System;
using InnoEngine.Mathematics;
using InnoEngine.Reflection;
using InnoEngine.Scene;
using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Interface;

namespace Inno.VisualNovel.Integration.Components.Interface;

/// <summary>Defines sizing and alignment without storing a second editable position beside the native Transform.</summary>
[StableTypeId("b0f32c52-b546-4259-9e9e-d4cd7ebc9c21")]
public sealed class NovelUiLayoutComponent : GameComponent
{
    /// <summary>Gets or sets the logical dimensions before native scale.</summary>
    [SerializableProperty] public Vector2 size { get; set; } = new(200, 48);
    /// <summary>Gets or sets horizontal sizing.</summary>
    [SerializableProperty] public UiSizeMode widthMode { get; set; }
    /// <summary>Gets or sets vertical sizing.</summary>
    [SerializableProperty] public UiSizeMode heightMode { get; set; }
    /// <summary>Gets or sets the lower size limits.</summary>
    [SerializableProperty] public Vector2 minimum { get; set; }
    /// <summary>Gets or sets the upper size limits.</summary>
    [SerializableProperty] public Vector2 maximum { get; set; } = new(100000, 100000);
    /// <summary>Gets or sets the normalized parent anchor for free positioning.</summary>
    [SerializableProperty] public Vector2 anchor { get; set; } = new(.5f, .5f);
    /// <summary>Gets or sets the normalized element pivot used by placement, scale and rotation.</summary>
    [SerializableProperty] public Vector2 pivot { get; set; } = new(.5f, .5f);
    /// <summary>Gets or sets left, top, right and bottom margins.</summary>
    [SerializableProperty] public Vector4 margin { get; set; }
    /// <summary>Gets or sets horizontal alignment in an automatic parent slot.</summary>
    [SerializableProperty] public UiAlignment horizontalAlignment { get; set; }
    /// <summary>Gets or sets vertical alignment in an automatic parent slot.</summary>
    [SerializableProperty] public UiAlignment verticalAlignment { get; set; }
    /// <summary>Gets or sets whether this box clips descendant drawing and input.</summary>
    [SerializableProperty] public bool clip { get; set; }
    /// <summary>Gets or sets opacity inherited by descendant controls.</summary>
    [SerializableProperty] public float opacity { get; set; } = 1;

    internal UiLayoutDefinition Capture()
    {
        float[] values = [size.x, size.y, minimum.x, minimum.y, maximum.x, maximum.y, anchor.x, anchor.y, pivot.x, pivot.y,
            margin.x, margin.y, margin.z, margin.w, opacity];
        if (Array.Exists(values, static value => !float.IsFinite(value)) || size.x < 0 || size.y < 0
            || minimum.x < 0 || minimum.y < 0 || maximum.x < minimum.x || maximum.y < minimum.y
            || margin.x < 0 || margin.y < 0 || margin.z < 0 || margin.w < 0 || opacity is < 0 or > 1
            || !Enum.IsDefined(widthMode) || !Enum.IsDefined(heightMode) || !Enum.IsDefined(horizontalAlignment) || !Enum.IsDefined(verticalAlignment))
            throw new InvalidOperationException("Invalid native UI layout: " + gameObject.name);
        return new()
        {
            width = size.x, height = size.y, widthMode = widthMode, heightMode = heightMode, minWidth = minimum.x, minHeight = minimum.y,
            maxWidth = maximum.x, maxHeight = maximum.y, anchorX = anchor.x, anchorY = anchor.y, pivotX = pivot.x, pivotY = pivot.y,
            marginLeft = margin.x, marginTop = margin.y, marginRight = margin.z, marginBottom = margin.w,
            horizontalAlignment = horizontalAlignment, verticalAlignment = verticalAlignment, clip = clip
        };
    }
}

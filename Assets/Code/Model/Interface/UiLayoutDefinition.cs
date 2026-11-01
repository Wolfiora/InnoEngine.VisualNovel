using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Interface;

/// <summary>Stores logical layout inputs without a rendering backend.</summary>
[GenerateSerializationConverter]
public sealed class UiLayoutDefinition : ISerializable
{
    /// <summary>Gets or sets the horizontal offset.</summary>
    [SerializableProperty] public float x { get; set; }
    /// <summary>Gets or sets the vertical offset.</summary>
    [SerializableProperty] public float y { get; set; }
    /// <summary>Gets or sets the requested width.</summary>
    [SerializableProperty] public float width { get; set; } = 200;
    /// <summary>Gets or sets the requested height.</summary>
    [SerializableProperty] public float height { get; set; } = 48;
    /// <summary>Gets or sets horizontal sizing.</summary>
    [SerializableProperty] public UiSizeMode widthMode { get; set; }
    /// <summary>Gets or sets vertical sizing.</summary>
    [SerializableProperty] public UiSizeMode heightMode { get; set; }
    /// <summary>Gets or sets the minimum width.</summary>
    [SerializableProperty] public float minWidth { get; set; }
    /// <summary>Gets or sets the minimum height.</summary>
    [SerializableProperty] public float minHeight { get; set; }
    /// <summary>Gets or sets the maximum width.</summary>
    [SerializableProperty] public float maxWidth { get; set; } = 100000;
    /// <summary>Gets or sets the maximum height.</summary>
    [SerializableProperty] public float maxHeight { get; set; } = 100000;
    /// <summary>Gets or sets the normalized horizontal anchor.</summary>
    [SerializableProperty] public float anchorX { get; set; }
    /// <summary>Gets or sets the normalized vertical anchor.</summary>
    [SerializableProperty] public float anchorY { get; set; }
    /// <summary>Gets or sets the normalized horizontal pivot.</summary>
    [SerializableProperty] public float pivotX { get; set; }
    /// <summary>Gets or sets the normalized vertical pivot.</summary>
    [SerializableProperty] public float pivotY { get; set; }
    /// <summary>Gets or sets container inset on all sides.</summary>
    [SerializableProperty] public float padding { get; set; }
    /// <summary>Gets or sets spacing between arranged children.</summary>
    [SerializableProperty] public float gap { get; set; } = 12;
    /// <summary>Gets or sets grid column count.</summary>
    [SerializableProperty] public int columns { get; set; } = 2;
    /// <summary>Gets or sets whether child drawing and input are clipped.</summary>
    [SerializableProperty] public bool clip { get; set; }
    /// <summary>Gets or sets the nonnegative space before this element's left edge.</summary>
    [SerializableProperty] public float marginLeft { get; set; }
    /// <summary>Gets or sets the nonnegative space before this element's top edge.</summary>
    [SerializableProperty] public float marginTop { get; set; }
    /// <summary>Gets or sets the nonnegative space after this element's right edge.</summary>
    [SerializableProperty] public float marginRight { get; set; }
    /// <summary>Gets or sets the nonnegative space after this element's bottom edge.</summary>
    [SerializableProperty] public float marginBottom { get; set; }
    /// <summary>Gets or sets horizontal alignment within an automatic layout slot.</summary>
    [SerializableProperty] public UiAlignment horizontalAlignment { get; set; }
    /// <summary>Gets or sets vertical alignment within an automatic layout slot.</summary>
    [SerializableProperty] public UiAlignment verticalAlignment { get; set; }
    /// <summary>Gets or sets distribution of this container's unused main-axis space; scroll containers retain leading-edge flow.</summary>
    [SerializableProperty] public UiFlowAlignment flowAlignment { get; set; }

    /// <summary>Copies the complete value-only layout contract without aliases or a second persistence format.</summary>
    /// <returns>A layout suitable for an independent runtime declaration or frozen editor gesture.</returns>
    public UiLayoutDefinition Copy() => new()
    {
        x = x, y = y, width = width, height = height, widthMode = widthMode, heightMode = heightMode,
        minWidth = minWidth, minHeight = minHeight, maxWidth = maxWidth, maxHeight = maxHeight,
        anchorX = anchorX, anchorY = anchorY, pivotX = pivotX, pivotY = pivotY, padding = padding, gap = gap, columns = columns, clip = clip,
        marginLeft = marginLeft, marginTop = marginTop, marginRight = marginRight, marginBottom = marginBottom,
        horizontalAlignment = horizontalAlignment, verticalAlignment = verticalAlignment, flowAlignment = flowAlignment
    };
}

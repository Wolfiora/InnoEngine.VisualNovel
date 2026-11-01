namespace Inno.VisualNovel.Model.Interface;

/// <summary>Specifies how an imported image occupies its authored bounds.</summary>
public enum UiImageFit
{
    /// <summary>Stretches the source to the full destination rectangle.</summary>
    Stretch,
    /// <summary>Preserves aspect ratio and shows the complete source centered inside the rectangle.</summary>
    Contain,
    /// <summary>Preserves aspect ratio, fills the rectangle and clips excess source around its center.</summary>
    Cover,
    /// <summary>Preserves declared source borders while stretching the interior image regions.</summary>
    NineSlice
}

namespace Inno.VisualNovel.Model.Interface;

/// <summary>Identifies controls implemented by the VN document runtime.</summary>
public enum UiElementKind
{
    /// <summary>A painted container.</summary>
    Panel,
    /// <summary>A textured element.</summary>
    Image,
    /// <summary>Shaped Unicode text.</summary>
    Text,
    /// <summary>A focusable action.</summary>
    Button,
    /// <summary>A Boolean value editor.</summary>
    Toggle,
    /// <summary>A bounded numeric value editor.</summary>
    Slider,
    /// <summary>A Unicode text editor.</summary>
    Input,
    /// <summary>A clipped scrollable container.</summary>
    Scroll,
    /// <summary>A horizontal stack.</summary>
    Horizontal,
    /// <summary>A vertical stack.</summary>
    Vertical,
    /// <summary>A regular cell grid.</summary>
    Grid,
    /// <summary>An unpainted layout element.</summary>
    Spacer,
    /// <summary>A noninteractive bounded numeric progress display.</summary>
    Progress,
    /// <summary>A typed selector with an owner-local modal dropdown.</summary>
    Selector,
    /// <summary>An aspect-aware clipped image with pointer zoom/drag and keyboard navigation.</summary>
    ImageViewport
}

/// <summary>Specifies how an element requests its size.</summary>
public enum UiSizeMode
{
    /// <summary>Uses the authored logical size.</summary>
    Fixed,
    /// <summary>Uses measured content.</summary>
    Content,
    /// <summary>Consumes available layout space.</summary>
    Fill
}

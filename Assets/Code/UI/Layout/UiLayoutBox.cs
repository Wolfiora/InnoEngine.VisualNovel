using System.Collections.Generic;
using InnoEngine.Mathematics;
using Inno.VisualNovel.Model.Interface;

namespace Inno.VisualNovel.UI.Layout;

/// <summary>Provides frame-local geometry to the shared layout algorithm without owning scene objects or persistent controls.</summary>
public abstract class UiLayoutBox
{
    /// <summary>Gets the current layout inputs, copied from the content owner when used for native scene capture.</summary>
    public abstract UiLayoutDefinition layout { get; }
    /// <summary>Gets the container arrangement or leaf control kind.</summary>
    public abstract UiElementKind kind { get; }
    /// <summary>Gets the text inset used during content measurement.</summary>
    public abstract float textPadding { get; }
    /// <summary>Gets whether the host should measure this box's text.</summary>
    public abstract bool hasText { get; }
    /// <summary>Gets current children in content-owner order. Native callers rebuild these from the real hierarchy each capture.</summary>
    public abstract IReadOnlyList<UiLayoutBox> layoutChildren { get; }
    /// <summary>Gets or sets whether the box participates in this layout pass.</summary>
    public bool visible { get; set; } = true;
    /// <summary>Gets or sets the arranged canvas rectangle.</summary>
    public Rect bounds { get; set; }
    /// <summary>Gets or sets the effective rectangle shared by painting and input.</summary>
    public Rect clip { get; set; }
    /// <summary>Gets or sets the measured width before parent allocation.</summary>
    public float measuredWidth { get; set; }
    /// <summary>Gets or sets the measured height before parent allocation.</summary>
    public float measuredHeight { get; set; }
    /// <summary>Gets or sets total content height for scroll-range computation.</summary>
    public float contentHeight { get; set; }
    /// <summary>Gets or sets the height of currently revealed text.</summary>
    public float revealHeight { get; set; }
    /// <summary>Gets or sets measured height of the currently revealed content.</summary>
    public float visibleMeasuredHeight { get; set; }
    /// <summary>Gets or sets total revealed content height.</summary>
    public float contentVisibleHeight { get; set; }
    /// <summary>Gets or sets the bounded scroll offset supplied by the actual control owner.</summary>
    public float scroll { get; set; }
    /// <summary>Gets or sets the previous revealed height used by follow-end scrolling.</summary>
    public float previousContentHeight { get; set; }
    /// <summary>Gets or sets whether the user has kept the scroll position at the tail.</summary>
    public bool followTail { get; set; } = true;
    /// <summary>Gets or sets whether new revealed content requests tail following.</summary>
    public bool followEnd { get; set; }
}

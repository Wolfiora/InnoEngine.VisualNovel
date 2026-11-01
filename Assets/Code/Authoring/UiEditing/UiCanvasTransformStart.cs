using InnoEngine.Mathematics;
using Inno.VisualNovel.Model.Interface;

namespace Inno.VisualNovel.Authoring.UiEditing;

/// <summary>Freezes one authored element's layout and actual runtime bounds for a complete native editor gesture.</summary>
public sealed class UiCanvasTransformStart
{
    internal UiCanvasTransformStart(
        string root,
        UiElementDefinition element,
        string parent,
        Rect bounds
    ) {
        this.root = root; this.elementId = element.id; this.parent = parent; this.bounds = bounds;
        layout = element.layout.Copy();
    }
    internal string root { get; }
    internal string parent { get; }
    internal UiLayoutDefinition layout { get; }
    /// <summary>Gets the stable authored element identity.</summary>
    public string elementId { get; }
    /// <summary>Gets the initial measured bounds in logical canvas coordinates.</summary>
    public Rect bounds { get; }
}

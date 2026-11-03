using InnoEngine.Mathematics;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.UI.Binding;

namespace Inno.VisualNovel.UI.Documents;

internal sealed class UiElementInstance(UiElementDefinition definition) : Layout.UiLayoutBox
{
    public override UiLayoutDefinition layout => definition.layout;
    public override UiElementKind kind => definition.kind;
    public override float textPadding => definition.style.textPadding;
    public override bool hasText => richText?.document.text.Length > 0 || text.Length > 0;
    public override System.Collections.Generic.IReadOnlyList<Layout.UiLayoutBox> layoutChildren => children;
    internal UiElementDefinition definition { get; } = definition;
    internal string id = definition.id;
    internal UiDataRow? row;
    internal UiElementInstance? parent;
    internal string repeatedKeys = "\0";
    internal Animation.UiElementAnimation animation = new();
    internal Matrix presentation = Matrix.identity;
    internal Rect visualBounds, visualClip, childClip;
    internal float visualOpacity = 1;
    internal bool inputVisible = true;
    internal bool waiting;
    internal string text = definition.text;
    internal string tooltip = string.Empty;
    internal readonly (float x, float y, float width, float height) authoredGeometry = (definition.layout.x, definition.layout.y, definition.layout.width, definition.layout.height);
    internal Inno.VisualNovel.UI.Text.UiRichText? richText;
    internal InnoEngine.Rendering.TextureAsset? image;
    internal Imaging.UiBitmap? bitmap;
    internal readonly Input.UiImageViewportController? imageViewport = definition.kind == UiElementKind.ImageViewport ? new() : null;
    internal readonly Controls.Inputs.UiTextInputController? textInput = definition.kind == UiElementKind.Input ? new() : null;
    internal bool enabled = true;
    internal bool behindStage;
    internal UiElementInstance[] children = [];
}

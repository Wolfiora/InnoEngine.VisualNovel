using InnoEditor.Rendering;
using InnoEngine.Rendering;
using Inno.VisualNovel.Integration.Rendering;

namespace Inno.VisualNovel.Editor.Presentation;

/// <summary>Uses the production VN rendering path in the editor Game viewport.</summary>
[EditorViewportContributorExtension("inno.vn.game-viewport", "inno.editor.viewport.game", order: 1000, controllerPriority: 100)]
public sealed class NovelGameViewportContributor : EditorViewportContributor
{
    private readonly NovelRenderModel m_model = new();
    /// <inheritdoc />
    public override bool CanContribute(EditorViewportContext context) => m_model.CanRender(Session(context));
    /// <inheritdoc />
    public override EditorViewportContribution Build(EditorViewportContext context)
    {
        RenderModelOutput frame = m_model.Build(Session(context));
        return new(frame.data, frame.pipeline, frame.targetFormat);
    }

    private static RenderOutputSession Session(EditorViewportContext context) => new(context.viewportId, context.content,
        new(0, 0, context.pixelWidth, context.pixelHeight), context.frameIndex, 0, context.viewContent, context.input);
}

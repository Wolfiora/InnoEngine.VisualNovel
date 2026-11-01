using System;
using InnoEngine.Rendering;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Rendering.Frames;
using Inno.VisualNovel.Rendering.Passes;
using Inno.VisualNovel.Stage.Composition;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Integration.Rendering;

/// <summary>Builds native frame data for isolated authoring previews using the production renderer.</summary>
public sealed class NovelPresentationFrame : IDisposable
{
    private readonly NovelFrameComposer m_composer = new();
    /// <summary>Gets the same pipeline used by scene-hosted VN games.</summary>
    public RenderPipelineAsset pipeline { get; } = new() { pipelineTypeId = "inno.vn.pipeline" };
    /// <summary>Extracts stage and UI into invocation-local native rendering data.</summary>
    /// <param name="ui">The resolved runtime UI frame.</param>
    /// <param name="material">The imported composition material.</param>
    /// <param name="stage">An optional stage frame.</param>
    /// <param name="targetWidth">Optional game design width independent of the UI page.</param>
    /// <param name="targetHeight">Optional game design height independent of the UI page.</param>
    /// <returns>Native frame channels consumed by the production pipeline.</returns>
    public RenderFrameData Build(
        UiFrame ui,
        MaterialAsset material,
        StageFrame? stage = null,
        float? targetWidth = null,
        float? targetHeight = null,
        MaterialAsset? effectsMaterial = null,
        UiFrame? previousUi = null
    ) {
        var data = new RenderFrameData();
        data.Set(NovelRenderPass.frameChannel, m_composer.Compose(ui, material, stage, targetWidth, targetHeight, effectsMaterial, previousUi));
        return data;
    }
    /// <summary>Measures text with the production engine shaper.</summary>
    /// <param name="element">The runtime declaration.</param>
    /// <param name="text">The resolved text.</param>
    /// <returns>Logical text size.</returns>
    public static (float width, float height) Measure(
        UiElementDefinition element,
        string text
    ) => NovelFrameComposer.Measure(element, text);
    /// <summary>Measures rich text using the same full-content layout as game rendering.</summary>
    /// <param name="element">The current declaration.</param>
    /// <param name="content">Full content and its independent reveal range.</param>
    /// <param name="resources">Page-owned alternate fonts and images.</param>
    /// <param name="width">Available inner text width.</param>
    /// <param name="scale">Accessibility scale for absolute scope sizes.</param>
    /// <returns>Stable full-text logical bounds.</returns>
    public static (float width, float height, float visibleHeight) MeasureRichText(
        UiElementDefinition element,
        Inno.VisualNovel.UI.Text.UiRichText content,
        Inno.VisualNovel.UI.Text.UiRichTextResources resources,
        float width,
        float scale
    ) => NovelFrameComposer.MeasureRichText(element, content, resources, width, scale);
    /// <summary>Releases generation-owned CPU glyph caches.</summary>
    public void Dispose() => m_composer.Clear();
}

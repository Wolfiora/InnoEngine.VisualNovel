using System;
using System.Numerics;
using InnoEditor.ImGui;
using InnoEditor.Rendering;
using InnoEngine.Rendering;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Integration.Rendering;
using Inno.VisualNovel.Stage.Composition;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Editor.Presentation;

internal sealed class NovelPreviewCanvas(IEditorPreviewService previews) : IDisposable
{
    private readonly string m_viewport = "inno.vn.authoring/" + Guid.NewGuid().ToString("N");
    private readonly NovelPresentationFrame m_frame = new();
    private MaterialAsset? m_material;
    private MaterialAsset? m_effects;
    internal void Draw(
        UiFrame ui,
        Vector2 size,
        StageFrame? stage = null,
        float? targetWidth = null,
        float? targetHeight = null,
        UiFrame? previousUi = null
    ) {
        m_material ??= new MaterialAsset { shader = BuiltInNovelAssets.Load<ShaderAsset>("BuiltIn/Shaders/Graphs/NovelSurface.ishader") };
        m_effects ??= new MaterialAsset { shader = BuiltInNovelAssets.Load<ShaderAsset>("BuiltIn/Shaders/Graphs/NovelPostProcess.ishader") };
        var data = m_frame.Build(ui, m_material, stage, targetWidth, targetHeight, m_effects, previousUi);
        var composition = new EditorViewportComposition(m_viewport, Math.Max(1, (int)size.X), Math.Max(1, (int)size.Y), RenderTextureFormat.RGBA8,
            [new("inno.vn.authoring", m_frame.pipeline, data, 0)]);
        if (previews.TryRender(composition, out EditorPreviewHandle handle)) previews.Draw(handle, size);
        else ImGui.Dummy(size);
    }
    public void Dispose() { previews.ReleaseRendered(m_viewport); m_frame.Dispose(); m_material = null; m_effects = null; }
}

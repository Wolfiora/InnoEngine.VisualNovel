using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Rendering;
using InnoEngine.Scene;
using Inno.VisualNovel.Integration.Hosting;
using Inno.VisualNovel.Rendering.Passes;

namespace Inno.VisualNovel.Integration.Rendering;

/// <summary>Renders an explicitly selected native VN scene in Edit or Play without discovering global worlds.</summary>
[RenderModelExtension("inno.vn.model")]
public sealed class NovelRenderModel : IRenderModel
{
    /// <inheritdoc />
    public bool CanRender(RenderOutputSession session) => Find(session) is not null;
    /// <inheritdoc />
    public RenderModelOutput Build(RenderOutputSession session)
    {
        NovelSceneSystem system = Find(session) ?? throw new InvalidOperationException("This output has no enabled Visual Novel scene system.");
        if (!system.hasPresentation) return BuildScene(system);
        var data = new RenderFrameData();
        data.Set(NovelRenderPass.frameChannel, system.Prepare(session));
        data.Set(Inno.VisualNovel.Rendering.Capture.NovelScreenshotService.channel, system.DrainCaptures());
        return new("Visual Novel", Pipeline(), data);
    }
    /// <summary>Builds passive scene output without starting a story or processing input.</summary>
    /// <param name="system">The native scene owner selected by the viewport.</param>
    /// <param name="view">Optional editor view; null uses the game's declared camera.</param>
    /// <returns>Frame-local native rendering channels.</returns>
    public static RenderModelOutput BuildScene(
        NovelSceneSystem system,
        NovelSceneProjection? view = null
    ) {
        var data = new RenderFrameData();
        data.Set(NovelRenderPass.frameChannel, system.CaptureScene(view));
        return new("Visual Novel", Pipeline(), data);
    }
    /// <summary>Selects the host's active root, or the sole eligible VN output when no active root is supplied.</summary>
    /// <param name="scenes">Only scenes explicitly supplied by this output's content scope.</param>
    /// <param name="activeScene">The host-selected primary scene identity.</param>
    /// <param name="requireGameOutput">True for Game output; false lets Scene View edit an intentionally non-output scene.</param>
    /// <returns>The enabled output owner, or null when the active root is not a VN scene.</returns>
    public static NovelSceneSystem? SelectSystem(
        IReadOnlyList<GameScene> scenes,
        Guid? activeScene,
        bool requireGameOutput = true
    ) {
        NovelSceneSystem? selected = null;
        foreach (GameScene scene in scenes)
        {
            if (scene.isDestroyed || !scene.isLoaded || activeScene.HasValue && scene.identity.persistentId != activeScene.Value) continue;
            foreach (NovelSceneSystem system in scene.GetSystems().OfType<NovelSceneSystem>())
            {
                if (!system.enabled || requireGameOutput && !system.gameOutput || system.isDestroyed) continue;
                if (selected is not null) throw new InvalidOperationException("Multiple Visual Novel outputs require an explicit active scene.");
                selected = system;
            }
        }
        return selected;
    }
    /// <inheritdoc />
    public void Dispose() { }
    private static RenderPipelineAsset Pipeline() => new() { pipelineTypeId = "inno.vn.pipeline" };
    private static NovelSceneSystem? Find(RenderOutputSession session)
        => SelectSystem(session.content.GetValues<GameScene>(), session.content.activeContent);
}
using System;
using System.Linq;
using InnoEngine.Graphs;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Model.Story.Nodes.Stage;

namespace Inno.VisualNovel.Authoring.Templates;

internal sealed class TemplateCanvasAdapter(
    float width,
    float height,
    StoryNodeDataCodec codec
)
{
    private readonly double m_scale = Math.Min(width / 1920d, height / 1080d);
    private double left => (width - 1920 * m_scale) / 2;
    private double top => (height - 1080 * m_scale) / 2;
    internal StageAsset Stage(StageAsset source)
    {
        var copy = StageAssetCopy.Copy(source); copy.width = width; copy.height = height;
        foreach (var layer in copy.layers) Place(layer.transform); return copy;
    }
    internal StoryDraftState Story(StoryDraftState draft)
    {
        foreach (var node in draft.graph.document.nodes.Where(static node => node.definitionId == Model.Story.StoryNodeIds.stage))
        {
            var settings = codec.Read<StageNode>(node);
            if (settings.operation == StageOperation.ShowCharacter) Place(settings.transform);
            else if (settings.operation == StageOperation.StartParticles) { settings.transform.x = settings.transform.x * m_scale + left; settings.transform.y = settings.transform.y * m_scale + top; }
            else if (settings.operation is StageOperation.SetCamera or StageOperation.FollowCamera) { settings.transform.x *= m_scale; settings.transform.y *= m_scale; }
            codec.Write(node, settings);
        }
        return draft;
    }
    internal PerformanceClipAsset Motion(PerformanceClipAsset source)
    {
        var copy = PerformanceClipCopy.Copy(source);
        foreach (var track in copy.tracks.Where(static track => track.property is PerformanceProperty.X or PerformanceProperty.Y or PerformanceProperty.ShakeX or PerformanceProperty.ShakeY))
            foreach (var key in track.keys) key.value *= m_scale;
        return copy;
    }
    internal VisualEffectAsset Particles(VisualEffectAsset source)
    {
        var copy = VisualEffectCopy.Copy(source); var p = copy.particles; p.width *= m_scale; p.height *= m_scale;
        p.minimumVelocityX *= m_scale; p.maximumVelocityX *= m_scale; p.minimumVelocityY *= m_scale; p.maximumVelocityY *= m_scale;
        p.accelerationX *= m_scale; p.accelerationY *= m_scale; p.startSize *= m_scale; p.endSize *= m_scale;
        foreach (var key in p.sizeKeys) key.value *= m_scale;
        return copy;
    }
    private void Place(StageTransform transform)
    {
        transform.x = transform.x * m_scale + left; transform.y = transform.y * m_scale + top; transform.scaleX *= m_scale; transform.scaleY *= m_scale;
    }
}

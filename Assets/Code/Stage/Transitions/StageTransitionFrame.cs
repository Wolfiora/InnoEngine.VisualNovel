using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Stage.Composition;

namespace Inno.VisualNovel.Stage.Transitions;

/// <summary>Contains a pure transition evaluation and the frozen previous stage frame.</summary>
/// <param name="sequence">The exact boundary used to associate a captured UI frame.</param>
/// <param name="kind">The image-composition mechanism.</param>
/// <param name="progress">Normalized logical completion.</param>
/// <param name="angle">Motion or reveal direction in degrees.</param>
/// <param name="softness">Normalized edge softness.</param>
/// <param name="color">Packed intermediate fade color.</param>
/// <param name="centerX">Normalized aperture center.</param>
/// <param name="centerY">Normalized aperture center.</param>
/// <param name="reverse">Whether the aperture reveals outside-in.</param>
/// <param name="mask">The procedural threshold pattern.</param>
/// <param name="seed">The deterministic mask seed.</param>
/// <param name="scope">The affected composition scope.</param>
/// <param name="group">The selected composition band.</param>
/// <param name="target">Optional exact owner filter.</param>
/// <param name="before">The frozen previous stage without a nested transition.</param>
public sealed record StageTransitionFrame(long sequence, TransitionKind kind, double progress, double angle, double softness, uint color,
    double centerX, double centerY, bool reverse, TransitionMask mask, uint seed, TransitionScope scope, StageLayerGroup group, string target, StageFrame before);

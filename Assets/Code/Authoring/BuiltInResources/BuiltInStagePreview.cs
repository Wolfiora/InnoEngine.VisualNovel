using System;
using System.Linq;
using InnoEngine.Assets;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Stage.Composition;

namespace Inno.VisualNovel.Authoring.BuiltInResources;

/// <summary>Evaluates a preset from explicit initial presentation state without story or external side effects.</summary>
public sealed class BuiltInStagePreview
{
    private readonly StageController m_stage;
    private readonly StageState m_initial;
    /// <summary>Creates a reusable preview using native presentation resources supplied by the authoring host.</summary>
    /// <param name="preset">A scenery, actor, motion or visual-effect preset.</param>
    /// <param name="scene">The fallback background.</param>
    /// <param name="actor">The fallback character used by motion previews.</param>
    /// <param name="nextScene">The destination for image transitions.</param>
    public BuiltInStagePreview(
        AssetObject preset,
        StageAsset scene,
        CharacterAsset actor,
        StageAsset nextScene
    ) {
        StageAsset background = preset as StageAsset ?? scene; CharacterAsset character = preset as CharacterAsset ?? actor;
        VisualEffectAsset? effect = preset as VisualEffectAsset; PerformanceClipAsset? motion = preset as PerformanceClipAsset;
        if (preset is not (StageAsset or CharacterAsset or VisualEffectAsset or PerformanceClipAsset)) throw new ArgumentException("This preset has no stage preview.", nameof(preset));
        m_stage = new(background.textures.Concat(character.textures).Concat(nextScene.textures), motion is null ? [] : [motion],
            effect?.kind == VisualEffectKind.Particles ? [effect] : [], effect?.kind == VisualEffectKind.Screen ? [effect] : [], effect?.kind == VisualEffectKind.Transition ? [effect] : []);
        m_stage.SetStage(background);
        if (preset is not StageAsset) m_stage.ShowCharacter("preview", character, character.defaultPose, character.defaultExpression, new() { x = 660, y = 130 }, 0);
        duration = 8;
        if (motion is not null)
        {
            m_stage.ApplyPerformance(new() { operation = PerformanceOperation.Play, clipId = motion.resourceId, instanceId = "preview", bindings = motion.tracks.Select(static track => track.target).Distinct(StringComparer.Ordinal)
                .Select(static slot => new PerformanceBinding { slot = slot, target = slot == "camera" ? "camera" : "character/preview" }).ToArray() }); duration = motion.duration;
        }
        if (effect?.kind == VisualEffectKind.Particles) m_stage.particles.Start("preview", effect.resourceId, 4107);
        if (effect?.kind == VisualEffectKind.Screen) m_stage.ApplyScreenEffect(new() { operation = ScreenEffectOperation.Set, resourceId = effect.resourceId });
        if (effect?.kind == VisualEffectKind.Transition) { m_stage.ApplyTransition(new() { operation = TransitionOperation.Begin, resourceId = effect.resourceId }, nextScene); duration = effect.transition.duration; }
        m_initial = m_stage.Capture();
    }
    /// <summary>Gets the useful preview playback interval in seconds.</summary>
    public double duration { get; }
    /// <summary>Evaluates a detached frame from the same initial state on every seek.</summary>
    /// <param name="seconds">A finite nonnegative absolute sample time.</param>
    /// <param name="reducedMotion">Whether optional movement is suppressed.</param>
    /// <returns>The normal runtime stage extraction, including image effects and transition state.</returns>
    public StageFrame Evaluate(
        double seconds,
        bool reducedMotion = false
    ) {
        StageController candidate = m_stage.Fork(m_initial); candidate.Tick(seconds, reducedMotion);
        return candidate.BuildFrame(1920, 1080);
    }
}

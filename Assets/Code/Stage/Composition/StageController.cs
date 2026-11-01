using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using InnoEngine.Rendering;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Stage.Timeline;
using Inno.VisualNovel.Stage.Effects;
using Inno.VisualNovel.Stage.Transitions;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Stage.Characters;
using Inno.VisualNovel.Stage.Camera;

namespace Inno.VisualNovel.Stage.Composition;

/// <summary>Owns layered scenery, character composition and a restorable camera without touching a GPU.</summary>
public sealed class StageController
{
    private readonly Dictionary<Guid, TextureAsset> m_textures;
    private readonly HashSet<Guid> m_textureIds;
    private CharacterAnimationController m_characters;
    private readonly PerformanceClipAsset[] m_clips;
    private readonly VisualEffectAsset[] m_presets;
    private readonly VisualEffectAsset[] m_screenPresets;
    private readonly VisualEffectAsset[] m_transitionPresets;
    private readonly Dictionary<string, PerformanceProperty[]> m_additionalTargets;
    private bool m_reducedMotion;
    private bool m_allowFlashing = true;
    private StageState m_state = new();
    /// <summary>Gets the owned deterministic stage performance controller.</summary>
    public StagePerformanceController performances { get; private set; }
    /// <summary>Gets this stage's deterministic particle owner.</summary>
    public StageParticleController particles { get; private set; }
    /// <summary>Gets deterministic post-processing instance ownership.</summary>
    public ScreenEffectController screenEffects { get; private set; }
    /// <summary>Gets the stage's single captured composition boundary.</summary>
    public StageTransitionController transitions { get; private set; }
    /// <summary>Gets whether any visible layer contributes to the stage.</summary>
    public bool hasContent
    {
        get
        {
            var values = performances.Evaluate();
            return m_state.layers.Any(layer => layer.visible && layer.surface.cropWidth > 0 && layer.surface.cropHeight > 0
                && layer.surface.sourceWidth > 0 && layer.surface.sourceHeight > 0 && layer.opacity > 0 && (layer.color & 255) > 0
                && (!values.TryGetValue((layer.owner, PerformanceProperty.Opacity), out double opacity) || opacity > 0))
                || particles.BuildBatches().Any(static batch => batch.samples.Count > 0);
        }
    }

    /// <summary>Creates a generation-local stage over an explicit resource closure.</summary>
    /// <param name="textures">Imported textures that may appear in saved stage states.</param>
    /// <param name="clips">An optional explicit performance resource library.</param>
    /// <param name="particlePresets">The optional native particle library.</param>
    /// <param name="screenPresets">The optional native screen-effect library.</param>
    /// <param name="transitionPresets">The optional native transition library.</param>
    /// <param name="additionalTargets">Explicit non-stage target IDs and scalar channel capabilities supplied by the session owner.</param>
    public StageController(
        IEnumerable<TextureAsset> textures,
        IEnumerable<PerformanceClipAsset>? clips = null,
        IEnumerable<VisualEffectAsset>? particlePresets = null,
        IEnumerable<VisualEffectAsset>? screenPresets = null,
        IEnumerable<VisualEffectAsset>? transitionPresets = null,
        IReadOnlyDictionary<string, PerformanceProperty[]>? additionalTargets = null
    ) {
        ArgumentNullException.ThrowIfNull(textures);
        m_textures = textures.DistinctBy(static texture => texture.identity.persistentId).ToDictionary(static texture => texture.identity.persistentId);
        if (m_textures.Values.Any(static texture => texture.isMissing || texture.identity.persistentId == Guid.Empty))
            throw new ArgumentException("Stage images must have persistent imported identities.", nameof(textures));
        m_textureIds = m_textures.Keys.ToHashSet(); m_characters = new([], [], m_textureIds, 0);
        m_clips = (clips ?? []).Select(PerformanceClipCopy.Copy).ToArray();
        m_presets = (particlePresets ?? []).Select(VisualEffectCopy.Copy).ToArray();
        m_screenPresets = (screenPresets ?? []).Select(VisualEffectCopy.Copy).ToArray();
        m_transitionPresets = (transitionPresets ?? []).Select(VisualEffectCopy.Copy).ToArray();
        m_additionalTargets = (additionalTargets ?? new Dictionary<string, PerformanceProperty[]>()).ToDictionary(static pair => pair.Key, static pair => pair.Value.ToArray(), StringComparer.Ordinal);
        if (m_additionalTargets.Keys.Any(static target => target == "camera" || target.StartsWith("stage/", StringComparison.Ordinal)
            || target.StartsWith("character/", StringComparison.Ordinal) || target.StartsWith("particles/", StringComparison.Ordinal)))
            throw new ArgumentException("Additional performance targets cannot replace native stage-owner namespaces.", nameof(additionalTargets));
        performances = new(m_clips, m_additionalTargets); particles = new(m_presets);
        screenEffects = new(m_screenPresets); transitions = new(m_transitionPresets);
    }

    /// <summary>Creates an independent evaluator over this resource closure and a validated neutral snapshot.</summary>
    /// <param name="snapshot">The exact resource-matching composition to evaluate independently.</param>
    /// <returns>A controller sharing imported resources while owning all mutable state.</returns>
    public StageController Fork(StageState snapshot)
    {
        var candidate = new StageController(m_textures.Values, m_clips, m_presets, m_screenPresets, m_transitionPresets, m_additionalTargets) { m_reducedMotion = m_reducedMotion, m_allowFlashing = m_allowFlashing };
        candidate.Restore(snapshot); return candidate;
    }

    /// <summary>Replaces scenery while retaining explicitly shown character layers.</summary>
    /// <param name="stage">The authored scenery.</param>
    public void SetStage(StageAsset stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        var layers = new List<StageLayerState>(m_state.layers.Where(static layer => layer.owner.StartsWith("character/", StringComparison.Ordinal)));
        foreach (StageLayer layer in stage.layers)
        {
            layer.surface.Validate();
            if (layer.texture < -1 || layer.texture >= stage.textures.Length || layer.mask < -1 || layer.mask >= stage.textures.Length)
                throw new InvalidOperationException("Stage source or mask image is unavailable: " + layer.id);
            Guid texture = layer.texture < 0 ? Guid.Empty : stage.textures[layer.texture].identity.persistentId;
            layers.Add(new()
            {
                id = "stage/" + layer.id, textureId = texture, width = layer.width, height = layer.height,
                maskTextureId = layer.mask < 0 ? Guid.Empty : stage.textures[layer.mask].identity.persistentId, surface = layer.surface.Copy(),
                owner = "stage/" + layer.id,
                pivotX = layer.transform.x, pivotY = layer.transform.y,
                matrix = Values(Matrix(layer.transform)), opacity = layer.transform.opacity, color = layer.color, group = layer.group,
                order = layer.order, parallax = layer.parallax, visible = layer.visible
            });
        }
        StageState candidate = Capture();
        var frozenCamera = FreezeRemovedCameraTarget(candidate, layers);
        candidate.layers = layers.ToArray();
        candidate.performances = performances.RetainTargets(candidate.performances, Targets(candidate.layers));
        RetainCameraPose(candidate, frozenCamera); Restore(candidate);
    }

    /// <summary>Shows or replaces a character instance using nondestructive pose and expression composition.</summary>
    /// <param name="id">Stable instance name used by later operations.</param>
    /// <param name="character">The imported layered character.</param>
    /// <param name="poseId">The selected pose, empty for the declared default.</param>
    /// <param name="expressionId">The selected expression, empty for the declared default.</param>
    /// <param name="transform">Placement in stage coordinates.</param>
    /// <param name="order">Composition order of this complete character instance.</param>
    public void ShowCharacter(
        string id,
        CharacterAsset character,
        string poseId,
        string expressionId,
        StageTransform transform,
        int order = 0
    ) {
        var composed = CharacterComposer.Compose(id, character, poseId, expressionId, transform, order, m_state.time);
        StageState candidate = Capture();
        var previous = candidate.characters.SingleOrDefault(item => item.owner == composed.animation.owner);
        if (previous?.speaking == true) { composed.animation.speaking = true; composed.animation.speechStarted = previous.speechStarted; }
        candidate.layers = [.. candidate.layers.Where(layer => layer.owner != composed.animation.owner), .. composed.layers];
        candidate.characters = [.. candidate.characters.Where(item => item.owner != composed.animation.owner), composed.animation]; Restore(candidate);
    }

    /// <summary>Removes all parts belonging to one character instance.</summary>
    /// <param name="id">The stable instance name.</param>
    public void HideCharacter(string id)
    {
        CharacterComposer.RequireInstance(id); string owner = "character/" + id;
        StageState candidate = Capture();
        var frozenCamera = FreezeRemovedCameraTarget(candidate, candidate.layers.Where(layer => layer.owner != owner));
        candidate.layers = candidate.layers.Where(layer => layer.owner != owner).ToArray();
        candidate.characters = candidate.characters.Where(item => item.owner != owner).ToArray();
        candidate.performances = performances.RetainTargets(candidate.performances, Targets(candidate.layers));
        RetainCameraPose(candidate, frozenCamera); Restore(candidate);
    }
    /// <summary>Begins or ends author-marked mouth animation without querying an audio device or phonemes.</summary>
    /// <param name="id">A currently shown character instance.</param>
    /// <param name="speaking">Whether authored mouth frames should repeat.</param>
    public void SetCharacterSpeaking(
        string id,
        bool speaking
    ) => m_characters.SetSpeaking(id, speaking, m_state.time);
    /// <summary>Reports whether a complete named character instance is currently present.</summary>
    /// <param name="id">The stable character instance name.</param>
    /// <returns>Whether at least one part belongs to that instance.</returns>
    public bool IsCharacterShown(string id) => m_state.layers.Any(layer => layer.owner == "character/" + id);

    /// <summary>Applies a pure lifecycle action against currently available stage owners.</summary>
    /// <param name="request">A detached play, pause, resume, cancel or seek request.</param>
    public void ApplyPerformance(Inno.VisualNovel.Model.Story.Nodes.Stage.PerformanceNode request) => performances.Apply(request, Targets(m_state.layers));
    /// <summary>Checks a named timeline boundary using the current motion policy.</summary>
    /// <param name="instanceId">The stable performance instance.</param>
    /// <param name="markerId">The clip-local marker identity.</param>
    /// <returns>Whether first marker arrival or cancellation has released the narrative wait.</returns>
    public bool HasReachedPerformanceMarker(
        string instanceId,
        string markerId
    ) => performances.HasReachedMarker(instanceId, markerId, m_reducedMotion);

    /// <summary>Applies one screen-effect action through its explicit instance owner.</summary>
    /// <param name="request">The typed lifecycle action.</param>
    public void ApplyScreenEffect(ScreenEffectNode request) => screenEffects.Apply(request);

    /// <summary>Prepares previous and new stage states before committing a transition lifecycle action.</summary>
    /// <param name="request">The explicit boundary action.</param>
    /// <param name="nextStage">Optional canonical scenery replacing the captured previous stage.</param>
    public void ApplyTransition(
        TransitionNode request,
        StageAsset? nextStage = null
    ) {
        if (!Enum.IsDefined(request.operation)) throw new InvalidOperationException("Transition operation is unavailable.");
        switch (request.operation)
        {
            case TransitionOperation.Begin:
                StageState before = Capture(); before.transitions = [];
                StageController candidate = Fork(before);
                if (nextStage is not null) candidate.SetStage(nextStage);
                candidate.transitions.Begin(request, before, candidate.Fork);
                Restore(candidate.Capture());
                break;
            case TransitionOperation.Cancel:
                StageState? previous = transitions.Previous();
                if (previous is not null) { previous.transitionSequence = transitions.sequence; Restore(previous); }
                break;
            case TransitionOperation.Complete: transitions.Complete(); break;
            case TransitionOperation.Pause: transitions.SetPaused(true); break;
            case TransitionOperation.Resume: transitions.SetPaused(false); break;
            case TransitionOperation.Wait: break;
        }
    }

    /// <summary>Replaces camera placement atomically.</summary>
    /// <param name="x">Horizontal offset.</param>
    /// <param name="y">Vertical offset.</param>
    /// <param name="zoom">Positive zoom.</param>
    /// <param name="rotation">Clockwise degrees.</param>
    public void SetCamera(double x, double y, double zoom, double rotation)
    {
        StageState candidate = Capture(); candidate.cameraX = x; candidate.cameraY = y; candidate.cameraZoom = zoom; candidate.cameraRotation = rotation;
        candidate.cameraFollow = null;
        Restore(candidate);
    }

    /// <summary>Follows an existing layer or character's authored pivot and timeline translation without using previous-frame feedback.</summary>
    /// <param name="owner">The qualified stage/layer or character/instance owner.</param>
    /// <param name="camera">Offsets, zoom and rotation; pivot fields specify the normalized screen focal anchor.</param>
    /// <param name="width">The game's logical design width.</param>
    /// <param name="height">The game's logical design height.</param>
    public void FollowCamera(
        string owner,
        StageTransform camera,
        double width,
        double height
    ) {
        StageState candidate = Capture(); candidate.cameraX = camera.x; candidate.cameraY = camera.y;
        candidate.cameraZoom = camera.scaleX; candidate.cameraRotation = camera.rotation;
        candidate.cameraFollow = new() { owner = owner, anchorX = camera.pivotX, anchorY = camera.pivotY, width = width, height = height };
        Restore(candidate);
    }
    /// <summary>Stops following while retaining the current logical framing and independently active camera channels.</summary>
    public void StopCameraFollow()
    {
        if (m_state.cameraFollow is null) return;
        var candidate = Capture(); StageCameraEvaluator.StopFollowing(candidate, performances.Evaluate()); Restore(candidate);
    }
    /// <summary>Gets the qualified owner currently followed by the camera, or empty for ordinary camera placement.</summary>
    public string cameraFollowOwner => m_state.cameraFollow?.owner ?? string.Empty;
    private (double x, double y, double zoom, double rotation)? FreezeRemovedCameraTarget(
        StageState candidate,
        IEnumerable<StageLayerState> retained
    ) {
        if (candidate.cameraFollow is not { } follow || retained.Any(layer => layer.owner == follow.owner)) return null;
        var samples = performances.Evaluate();
        var pose = StageCameraEvaluator.Evaluate(candidate, samples, follow.width, follow.height);
        StageCameraEvaluator.StopFollowing(candidate, samples); return pose;
    }
    private void RetainCameraPose(
        StageState candidate,
        (double x, double y, double zoom, double rotation)? pose
    ) {
        if (pose is null) return;
        var retained = new StagePerformanceController(m_clips, m_additionalTargets);
        retained.Restore(candidate.performances, Targets(candidate.layers)); retained.Tick(0, m_reducedMotion);
        StageCameraEvaluator.ApplyPose(candidate, pose.Value, retained.Evaluate());
    }

    /// <summary>Advances deterministic logical time.</summary>
    /// <param name="seconds">Finite nonnegative interval.</param>
    /// <param name="reducedMotion">Whether optional motion completes without animation.</param>
    /// <param name="allowFlashing">Whether the player allows full-frame flashes.</param>
    public void Tick(
        double seconds,
        bool reducedMotion = false,
        bool allowFlashing = true
    ) {
        if (!double.IsFinite(seconds) || seconds < 0 || !double.IsFinite(m_state.time + seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        particles.Tick(seconds, reducedMotion); performances.Tick(seconds, reducedMotion); m_state.time += seconds; m_reducedMotion = reducedMotion;
        screenEffects.Tick(seconds, reducedMotion); transitions.Tick(seconds, reducedMotion, allowFlashing); m_allowFlashing = allowFlashing;
    }

    /// <summary>Extracts transformed images in deterministic composition order.</summary>
    /// <param name="width">Design canvas width.</param>
    /// <param name="height">Design canvas height.</param>
    /// <returns>A detached frame with no GPU handles.</returns>
    public StageFrame BuildFrame(float width, float height)
    {
        if (!float.IsFinite(width) || !float.IsFinite(height) || width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        var items = new List<(StageLayerGroup group, int order, string owner, int partOrder, string id, StageDrawItem draw)>();
        var samples = performances.Evaluate();
        var cameraPose = StageCameraEvaluator.Evaluate(m_state, samples, width, height);
        double cameraX = cameraPose.x, cameraY = cameraPose.y, cameraZoom = cameraPose.zoom, cameraRotation = cameraPose.rotation;
        foreach (StageLayerState layer in m_state.layers.Where(static layer => layer.visible).OrderBy(static layer => layer.group).ThenBy(static layer => layer.order)
            .ThenBy(static layer => layer.owner, StringComparer.Ordinal).ThenBy(static layer => layer.partOrder).ThenBy(static layer => layer.id, StringComparer.Ordinal))
        {
            StageSurface surface = layer.surface;
            if (surface.cropWidth == 0 || surface.cropHeight == 0 || surface.sourceWidth == 0 || surface.sourceHeight == 0) continue;
            float left = (float)(layer.width * surface.cropX), top = (float)(layer.height * surface.cropY);
            float right = (float)(layer.width * (surface.cropX + surface.cropWidth)), bottom = (float)(layer.height * (surface.cropY + surface.cropHeight));
            Matrix3x2 camera = Matrix3x2.CreateTranslation((float)(-cameraX * layer.parallax), (float)(-cameraY * layer.parallax))
                * Matrix3x2.CreateTranslation(-width / 2, -height / 2) * Matrix3x2.CreateScale((float)cameraZoom)
                * Matrix3x2.CreateRotation((float)(cameraRotation * Math.PI / 180)) * Matrix3x2.CreateTranslation(width / 2, height / 2);
            Matrix3x2 motion = Matrix3x2.CreateTranslation((float)-layer.pivotX, (float)-layer.pivotY)
                * Matrix3x2.CreateScale((float)Value(layer.owner, PerformanceProperty.ScaleX, 1), (float)Value(layer.owner, PerformanceProperty.ScaleY, 1))
                * Matrix3x2.CreateRotation((float)(Value(layer.owner, PerformanceProperty.Rotation, 0) * Math.PI / 180))
                * Matrix3x2.CreateTranslation((float)(layer.pivotX + Offset(layer.owner, PerformanceProperty.X, PerformanceProperty.ShakeX)), (float)(layer.pivotY + Offset(layer.owner, PerformanceProperty.Y, PerformanceProperty.ShakeY)));
            var character = m_characters.Evaluate(layer, m_state.time, m_reducedMotion);
            Matrix3x2 transform = Matrix(layer.matrix) * character.motion * motion * camera;
            uint color = layer.color & 0xFFFFFF00 | (uint)Math.Round((layer.color & 255) * layer.opacity * Value(layer.owner, PerformanceProperty.Opacity, 1));
            items.Add((layer.group, layer.order, layer.owner, layer.partOrder, layer.id, new(layer.id, Vector2.Transform(new(left, top), transform), Vector2.Transform(new(right, top), transform),
                Vector2.Transform(new(right, bottom), transform), Vector2.Transform(new(left, bottom), transform),
                character.texture == Guid.Empty ? null : m_textures[character.texture], color, layer.group, layer.owner, layer.order,
                new((float)(surface.sourceX + surface.cropX * surface.sourceWidth), (float)(surface.sourceY + surface.cropY * surface.sourceHeight),
                    (float)(surface.sourceWidth * surface.cropWidth), (float)(surface.sourceHeight * surface.cropHeight)),
                surface.blend, layer.maskTextureId == Guid.Empty ? null : m_textures[layer.maskTextureId], surface.maskMode,
                new((float)surface.cropX, (float)surface.cropY, (float)surface.cropWidth, (float)surface.cropHeight))));
        }
        foreach (ParticleDrawBatch batch in particles.BuildBatches())
        {
            Matrix3x2 camera = Matrix3x2.CreateTranslation((float)(-cameraX * batch.parallax), (float)(-cameraY * batch.parallax))
                * Matrix3x2.CreateTranslation(-width / 2, -height / 2) * Matrix3x2.CreateScale((float)cameraZoom)
                * Matrix3x2.CreateRotation((float)(cameraRotation * Math.PI / 180)) * Matrix3x2.CreateTranslation(width / 2, height / 2);
            int index = 0;
            foreach (StageDrawItem particle in ParticleFrameBuilder.Build(batch, camera)) items.Add((batch.group, batch.order, "particles/" + batch.id, index++, particle.id,
                particle with { group = batch.group, owner = "particles/" + batch.id, order = batch.order }));
        }
        return new(items.OrderBy(static item => item.group).ThenBy(static item => item.order).ThenBy(static item => item.owner, StringComparer.Ordinal)
            .ThenBy(static item => item.partOrder).ThenBy(static item => item.id, StringComparer.Ordinal).Select(static item => item.draw).ToArray(),
            screenEffects.BuildFrame(m_allowFlashing && !m_reducedMotion, samples), transitions.BuildFrame(width, height));
        double Offset(
            string target,
            PerformanceProperty position,
            PerformanceProperty shake
        ) => Value(target, position, 0) + Value(target, shake, 0);
        double Value(
            string target,
            PerformanceProperty property,
            double fallback
        ) => samples.TryGetValue((target, property), out double value) ? value : fallback;
    }

    /// <summary>Captures independent neutral stage state.</summary>
    /// <returns>A deep copy of logical composition.</returns>
    public StageState Capture()
    {
        StageState result = Copy(m_state); result.characters = m_characters.Capture(); result.performances = performances.Capture(); result.particles = particles.Capture();
        result.screenEffects = screenEffects.Capture(); result.transitions = transitions.Capture(); result.transitionSequence = transitions.sequence; return result;
    }

    /// <summary>Validates a complete candidate before changing the stage.</summary>
    /// <param name="state">Neutral state with resources from the current closure.</param>
    public void Restore(StageState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!double.IsFinite(state.time) || state.time < 0 || !double.IsFinite(state.cameraX) || !double.IsFinite(state.cameraY)
            || !double.IsFinite(state.cameraZoom) || state.cameraZoom <= 0 || state.cameraZoom > 1e6 || !double.IsFinite(state.cameraRotation)
            || Math.Abs(state.cameraX) > 1e12 || Math.Abs(state.cameraY) > 1e12 || Math.Abs(state.cameraRotation) > 1e12
            || state.layers.Length > 10000 || state.layers.Select(static layer => layer.id).Distinct(StringComparer.Ordinal).Count() != state.layers.Length)
            throw new InvalidOperationException("Stage camera or layer identities are invalid.");
        foreach (StageLayerState layer in state.layers)
        {
            layer.surface.Validate();
            if (string.IsNullOrWhiteSpace(layer.id) || !Enum.IsDefined(layer.group) || layer.matrix.Length != 6 || layer.matrix.Any(static value => !double.IsFinite(value) || Math.Abs(value) > 1e12)
                || !double.IsFinite(layer.width) || !double.IsFinite(layer.height) || layer.width < 0 || layer.height < 0 || layer.width > 1e9 || layer.height > 1e9
                || !double.IsFinite(layer.opacity) || layer.opacity is < 0 or > 1 || !double.IsFinite(layer.parallax) || Math.Abs(layer.parallax) > 1e6
                || !double.IsFinite(layer.pivotX) || !double.IsFinite(layer.pivotY) || Math.Abs(layer.pivotX) > 1e12 || Math.Abs(layer.pivotY) > 1e12
                || layer.textureId != Guid.Empty && !m_textures.ContainsKey(layer.textureId)
                || layer.maskTextureId != Guid.Empty && !m_textures.ContainsKey(layer.maskTextureId))
                throw new InvalidOperationException("Stage layer has invalid geometry or unavailable image: " + layer.id);
        }
        if (state.layers.Count(static layer => layer.maskTextureId != Guid.Empty || layer.surface.blend != StageBlendMode.Normal) > 48)
            throw new InvalidOperationException("A stage supports at most 48 masked or non-normal image layers per composition.");
        StageCameraEvaluator.Validate(state);
        StageState candidate = Copy(state);
        var characterCandidate = new CharacterAnimationController(candidate.characters, candidate.layers, m_textureIds, candidate.time);
        var performanceCandidate = new StagePerformanceController(m_clips, m_additionalTargets);
        var particleCandidate = new StageParticleController(m_presets);
        var screenCandidate = new ScreenEffectController(m_screenPresets);
        var transitionCandidate = new StageTransitionController(m_transitionPresets);
        performanceCandidate.Restore(state.performances, Targets(candidate.layers));
        performanceCandidate.Tick(0, m_reducedMotion);
        particleCandidate.Restore(state.particles); particleCandidate.Tick(0, m_reducedMotion);
        screenCandidate.Restore(state.screenEffects); screenCandidate.Tick(0, m_reducedMotion);
        transitionCandidate.Restore(state.transitions, state.transitionSequence, Fork); transitionCandidate.Tick(0, m_reducedMotion, m_allowFlashing);
        m_state = candidate; m_characters = characterCandidate; performances = performanceCandidate; particles = particleCandidate; screenEffects = screenCandidate; transitions = transitionCandidate;
    }

    private static Matrix3x2 Matrix(StageTransform transform) => StageAffineTransform.From(transform);
    private static Matrix3x2 Matrix(double[] value) => StageAffineTransform.From(value);
    private static double[] Values(Matrix3x2 value) => StageAffineTransform.Values(value);
    private static StageState Copy(StageState state) => new()
    {
        cameraX = state.cameraX, cameraY = state.cameraY, cameraZoom = state.cameraZoom, cameraRotation = state.cameraRotation, time = state.time,
        cameraFollow = state.cameraFollow?.Copy(),
        characters = state.characters.Select(static character => character.Copy()).ToArray(),
        layers = state.layers.Select(static layer => new StageLayerState
        {
            id = layer.id, textureId = layer.textureId, maskTextureId = layer.maskTextureId, surface = layer.surface.Copy(),
            width = layer.width, height = layer.height, matrix = [.. layer.matrix], opacity = layer.opacity,
            color = layer.color, group = layer.group, order = layer.order, owner = layer.owner, partOrder = layer.partOrder, parallax = layer.parallax, visible = layer.visible,
            pivotX = layer.pivotX, pivotY = layer.pivotY
        }).ToArray()
    };
    private string[] Targets(StageLayerState[] layers) => layers.Select(static layer => layer.owner).Append("camera").Concat(m_additionalTargets.Keys).Distinct(StringComparer.Ordinal).ToArray();
}

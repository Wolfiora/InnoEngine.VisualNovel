using System;
using System.Linq;
using InnoEngine.Scene;
using InnoEngine.Rendering;
using InnoEngine.Mathematics;
using InnoEditor.Scene;
using InnoEditor.Interactions;
using InnoEditor.Inspection;
using Inno.VisualNovel.Integration.Hosting;
using Inno.VisualNovel.Integration.Components;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Stages;

namespace Inno.VisualNovel.Authoring.SceneEditing;

/// <summary>Creates native scene content through the engine's reversible scene and property edits.</summary>
public sealed class NovelSceneEditService
{
    private readonly SceneEdits m_scenes;
    private readonly IInspectionPropertyEditService m_properties;

    /// <summary>Creates a scene authoring service owned by the current editor generation.</summary>
    /// <param name="scenes">The native scene edit service.</param>
    /// <param name="properties">The native property-edit transaction service.</param>
    public NovelSceneEditService(
        SceneEdits scenes,
        IInspectionPropertyEditService properties
    ) {
        m_scenes = scenes; m_properties = properties;
    }

    /// <summary>Creates a native game scene, scene system, camera and independently editable initial background objects.</summary>
    /// <param name="game">The imported game definition selected by the creation wizard.</param>
    /// <returns>A native scene ready for saving through the engine workspace.</returns>
    public GameScene CreateGameScene(NovelProjectAsset game)
    {
        ArgumentNullException.ThrowIfNull(game);
        StageAsset? initial = game.initialStage.Length == 0 ? null : game.stages.SingleOrDefault(stage => stage.resourceId == game.initialStage);
        if (game.initialStage.Length > 0 && initial is null) throw new InvalidOperationException("The initial stage is not declared in this game.");
        GameScene scene = m_scenes.CreateScene("Create Visual Novel scene");
        m_scenes.RenameScene(scene, "NovelStart");
        var system = (NovelSceneSystem)m_scenes.AddSystem(scene, typeof(NovelSceneSystem));
        Change(system, nameof(system.project), () => system.project = game);
        Change(system, nameof(system.surfaceMaterial), () => system.surfaceMaterial = game.material);
        Change(system, nameof(system.imageEffectsMaterial), () => system.imageEffectsMaterial = game.effectsMaterial);
        GameObject cameraObject = m_scenes.CreateGameObject(scene);
        m_scenes.RenameGameObject(cameraObject, "Game Camera");
        var camera = (NovelCameraComponent)m_scenes.AddComponent(cameraObject, typeof(NovelCameraComponent));
        Change(system, nameof(system.camera), () => system.camera = camera);
        if (initial is not null) AddInitialScenery(scene, initial);
        return scene;
    }

    /// <summary>Explicitly binds the scene's declared target components as one reversible native property edit.</summary>
    /// <param name="system">The scene-owned story binding scope; duplicates fail before any property changes.</param>
    public void BindSceneTargets(NovelSceneSystem system)
    {
        ArgumentNullException.ThrowIfNull(system);
        _ = Inno.VisualNovel.Integration.Rendering.NovelSceneCapture.Targets(system.owningScene);
        var targets = system.owningScene.GetObjects().SelectMany(static item => item.GetComponents<NovelTargetComponent>())
            .Where(static item => !string.IsNullOrWhiteSpace(item.targetId)).OrderBy(static item => item.targetId, StringComparer.Ordinal).ToArray();
        if (targets.Length > 10000 || targets.Any(static target => target.targetId.Length > 256))
            throw new InvalidOperationException("Use at most 10000 scene targets, with identities of at most 256 characters.");
        Change(system, nameof(system.targets), () => system.targets = targets);
    }

    /// <summary>Creates an independently editable image object with reversible native creation and property steps.</summary>
    /// <param name="scene">The destination scene.</param>
    /// <param name="name">The object name shown in Hierarchy.</param>
    /// <param name="texture">An imported image, or null for a solid surface.</param>
    /// <param name="size">Positive local dimensions in design pixels.</param>
    /// <param name="parent">Optional native parent in the destination scene.</param>
    /// <returns>The actual image component attached to the new native object.</returns>
    public NovelImageComponent CreateImage(
        GameScene scene,
        string name,
        TextureAsset? texture,
        Vector2 size,
        Transform? parent = null
    ) {
        if (!float.IsFinite(size.x) || size.x <= 0 || !float.IsFinite(size.y) || size.y <= 0)
            throw new ArgumentOutOfRangeException(nameof(size));
        GameObject item = m_scenes.CreateGameObject(scene, parent);
        m_scenes.RenameGameObject(item, name);
        var image = (NovelImageComponent)m_scenes.AddComponent(item, typeof(NovelImageComponent));
        Change(image, nameof(image.texture), () => image.texture = texture);
        Change(image, nameof(image.width), () => image.width = size.x);
        Change(image, nameof(image.height), () => image.height = size.y);
        return image;
    }

    /// <summary>Creates a real scene audio object whose cue and gain are editable in native Inspector.</summary>
    /// <param name="scene">The destination scene.</param>
    /// <returns>The new audio component; assigning a cue enables playback on the next runtime activation.</returns>
    public NovelAudioComponent CreateAudio(GameScene scene)
    {
        var owner = m_scenes.CreateGameObject(scene);
        m_scenes.RenameGameObject(owner, "Audio");
        return (NovelAudioComponent)m_scenes.AddComponent(owner, typeof(NovelAudioComponent));
    }

    /// <summary>Creates and assigns a camera through reversible native scene operations.</summary>
    /// <param name="system">The output that will use the new camera.</param>
    /// <returns>The new scene-owned camera.</returns>
    public NovelCameraComponent CreateCamera(NovelSceneSystem system)
    {
        ArgumentNullException.ThrowIfNull(system);
        var camera = CreateComponent<NovelCameraComponent>(system.owningScene, "Game Camera");
        Change(system, nameof(system.camera), () => system.camera = camera);
        return camera;
    }
    /// <summary>Creates a character root whose resource and real image parts are configured in Inspector.</summary>
    /// <param name="scene">The destination scene.</param>
    /// <returns>The component on a new native object.</returns>
    public NovelCharacterComponent CreateCharacter(GameScene scene) => CreateComponent<NovelCharacterComponent>(scene, "Character");
    /// <summary>Creates a native emitter ready for an imported particle preset.</summary>
    /// <param name="scene">The destination scene.</param>
    /// <returns>The scene-owned emitter.</returns>
    public NovelParticleComponent CreateParticles(GameScene scene) => CreateComponent<NovelParticleComponent>(scene, "Particles");
    /// <summary>Creates an effect object with editable preset and instance parameters.</summary>
    /// <param name="scene">The destination scene.</param>
    /// <returns>The scene-owned compositing component.</returns>
    public NovelScreenEffectComponent CreateScreenEffect(GameScene scene) => CreateComponent<NovelScreenEffectComponent>(scene, "Screen Effect");
    /// <summary>Creates a stopped performance owner with explicit clip and native target bindings.</summary>
    /// <param name="scene">The destination scene.</param>
    /// <returns>The component; no track is applied until explicitly sampled or played.</returns>
    public NovelPerformanceComponent CreatePerformance(GameScene scene) => CreateComponent<NovelPerformanceComponent>(scene, "Performance");

    private T CreateComponent<T>(
        GameScene scene,
        string name
    ) where T : GameComponent
    {
        var item = m_scenes.CreateGameObject(scene);
        m_scenes.RenameGameObject(item, name);
        return (T)m_scenes.AddComponent(item, typeof(T));
    }

    private void AddInitialScenery(
        GameScene scene,
        StageAsset stage
    ) {
        GameObject group = m_scenes.CreateGameObject(scene);
        m_scenes.RenameGameObject(group, "Background");
        foreach (StageLayer layer in stage.layers)
        {
            layer.surface.Validate(); layer.transform.Validate();
            if (layer.texture < -1 || layer.texture >= stage.textures.Length || layer.mask < -1 || layer.mask >= stage.textures.Length)
                throw new InvalidOperationException("The initial background contains a missing image reference: " + layer.id);
            var image = CreateImage(scene, layer.name, layer.texture < 0 ? null : stage.textures[layer.texture],
                new((float)layer.width, (float)layer.height), group.transform);
            Change(image, nameof(image.visible), () => image.visible = layer.visible);
            Change(image, nameof(image.pivot), () => image.pivot = new((float)(layer.transform.pivotX / layer.width), (float)(layer.transform.pivotY / layer.height)));
            Change(image, nameof(image.uv), () => image.uv = new((float)layer.surface.sourceX, (float)layer.surface.sourceY,
                (float)layer.surface.sourceWidth, (float)layer.surface.sourceHeight));
            Change(image, nameof(image.crop), () => image.crop = new((float)layer.surface.cropX, (float)layer.surface.cropY,
                (float)layer.surface.cropWidth, (float)layer.surface.cropHeight));
            Change(image, nameof(image.color), () => image.color = (layer.color & 0xFFFFFF00)
                | (uint)Math.Round((layer.color & 255) * Math.Clamp(layer.transform.opacity, 0, 1)));
            Change(image, nameof(image.order), () => image.order = layer.order);
            Change(image, nameof(image.parallax), () => image.parallax = (float)layer.parallax);
            Change(image, nameof(image.group), () => image.group = layer.group);
            Change(image, nameof(image.blend), () => image.blend = layer.surface.blend);
            Change(image, nameof(image.mask), () => image.mask = layer.mask < 0 ? null : stage.textures[layer.mask]);
            Change(image, nameof(image.maskMode), () => image.maskMode = layer.surface.maskMode);
            Transform transform = image.transform;
            Change(transform, nameof(transform.localPosition), () => transform.localPosition = new((float)(layer.transform.x - stage.width * .5), (float)(stage.height * .5 - layer.transform.y), 0));
            Change(transform, nameof(transform.localRotation), () => transform.localRotation = Quaternion.FromEulerAnglesXYZDegrees(new(0, 0, (float)-layer.transform.rotation)));
            Change(transform, nameof(transform.localScale), () => transform.localScale = new((float)layer.transform.scaleX, (float)layer.transform.scaleY, 1));
        }
    }

    private void Change(
        object owner,
        string property,
        Action edit
    ) => m_properties.ChangeProperty(owner, property, edit, "Configure VN scene", null, null);
}

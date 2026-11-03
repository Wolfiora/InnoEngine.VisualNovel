using System;
using System.Linq;
using Inno.Core.Identity;
using Inno.Core.Mathematics;
using Inno.Extensibility.Types;
using Inno.Rendering.Assets;
using Inno.Scene;
using Inno.VisualNovel.Integration.Components;
using Inno.VisualNovel.Integration.Hosting;
using Inno.VisualNovel.Integration.Rendering;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Stages;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class NativeSceneEffectsContracts
{
    internal static void ComponentClocksAndParentOwnership(
        IdentityAllocator identities,
        TypeCatalog types
    ) {
        using var world = new SceneWorld(identities, types); using var scope = world.EnterScope();
        var scene = SceneManager.LoadNewSceneAdditive("Native effects");
        var parent = scene.CreateObject("Effect group");
        var emitter = scene.CreateObject("Particles").AddComponent<NovelParticleComponent>();
        emitter.transform.SetParent(parent.transform);
        emitter.preset = new() { resourceId = "test.particles", kind = VisualEffectKind.Particles,
            particles = new() { rate = 2, width = 0, height = 0, minimumLifetime = 2, maximumLifetime = 2,
                minimumVelocityX = 0, maximumVelocityX = 0, minimumVelocityY = 0, maximumVelocityY = 0,
                startSize = 10, endSize = 10, minimumRotation = 0, maximumRotation = 0, angularSpeed = 0 } };
        var effect = scene.CreateObject("Fade").AddComponent<NovelScreenEffectComponent>(); effect.transform.SetParent(parent.transform);
        effect.preset = new() { resourceId = "test.effect", kind = VisualEffectKind.Screen };
        effect.from = 0; effect.strength = 1; effect.duration = 2;
        var view = new NovelSceneProjection(800, 600, Matrix.identity);
        Check(NovelSceneCapture.Capture(scene, view).items.Count == 0 && emitter.elapsed == 0 && effect.elapsed == 0,
            "Passive capture advanced native component clocks.");
        NovelSceneSimulation.Tick(scene, .5);
        var first = NovelSceneCapture.Capture(scene, view);
        Check(first.items.Count == 1 && first.effects?.Single().strength == .25 && emitter.elapsed == .5,
            "Native effects did not use the explicit scene update clock.");
        parent.transform.localPosition = new(100, 20, 0);
        var moved = NovelSceneCapture.Capture(scene, view);
        Check(moved.items.Single().topLeft - first.items.Single().topLeft == new System.Numerics.Vector2(100, -20),
            "The particle emitter ignored its actual parent transform.");
        parent.SetActive(false); NovelSceneSimulation.Tick(scene, 1);
        var disabled = NovelSceneCapture.Capture(scene, view);
        Check(disabled.items.Count == 0 && disabled.effects?.Count == 0 && emitter.elapsed == .5 && effect.elapsed == .5,
            "An inactive parent retained visible or advancing effect owners.");
        parent.SetActive(true); emitter.StopEmission(); NovelSceneSimulation.Tick(scene, 3);
        Check(NovelSceneCapture.Capture(scene, view).items.Count == 0, "Stopped native emission kept creating particles.");
        emitter.Restart(); NovelSceneSimulation.Tick(scene, .5);
        Check(NovelSceneCapture.Capture(scene, view).items.Count == 1, "A native emitter could not restart independently.");
        scene.DestroyObject(emitter.gameObject); scene.DestroyObject(effect.gameObject);
        var deleted = NovelSceneCapture.Capture(scene, view);
        Check(deleted.items.Count == 0 && deleted.effects?.Count == 0, "Deleted effect objects survived in a private presentation tree.");
    }

    internal static void RealCharacterPartsAndAtomicAppearance(
        IdentityAllocator identities,
        TypeCatalog types
    ) {
        using var world = new SceneWorld(identities, types); using var scope = world.EnterScope();
        var scene = SceneManager.LoadNewSceneAdditive("Native character");
        var still = new TextureAsset(1, 1, TextureColorSpace.Srgb, "png"); var talking = new TextureAsset(1, 1, TextureColorSpace.Srgb, "png");
        var actor = scene.CreateObject("Actor").AddComponent<NovelCharacterComponent>();
        actor.character = new() { resourceId = "actor", textures = [still, talking], expressions = [], defaultExpression = "",
            poses = [new() { id = "default", parts = [new() { id = "face", width = 100, height = 80,
                transform = new() { x = 10, y = 20, pivotX = 50, pivotY = 40 },
                animation = new() { mode = CharacterAnimationMode.Talking, frames = [new() { texture = 1, seconds = .2 }], stagger = false } }] }] };
        var faceObject = scene.CreateObject("Real face"); faceObject.transform.SetParent(actor.transform);
        var image = faceObject.AddComponent<NovelImageComponent>();
        var part = faceObject.AddComponent<NovelCharacterPartComponent>(); part.character = actor; part.partId = "face";
        actor.ApplyAppearance("default", "");
        Check(image.texture == still && image.pivot == new Vector2(.5f, .5f) && faceObject.transform.localPosition == new Vector3(10, -20, 0),
            "Appearance did not configure the real image, normalized pivot or native transform.");
        var view = new NovelSceneProjection(800, 600, Matrix.identity);
        actor.SetSpeaking(true); NovelSceneSimulation.Tick(scene, .1);
        Check(NovelSceneCapture.Capture(scene, view).items.Single().texture == talking && image.texture == still,
            "Sprite evaluation did not use the speaking marker or mutated the authored still image.");
        actor.SetSpeaking(false); faceObject.transform.localPosition = new(200, 0, 0);
        Check(NovelSceneCapture.Capture(scene, view).items.Single().topLeft == new System.Numerics.Vector2(550, 260),
            "Rendering overwrote an external script's native part transform.");
        actor.breathing.enabled = true; actor.transform.localScale = Vector3.ZERO;
        Check(NovelSceneCapture.HitTest(scene, view, new(400, 300)) is null,
            "Breathing under a zero-scale native parent captured input or required an invalid matrix inverse.");
        actor.transform.localScale = Vector3.ONE; actor.breathing.enabled = false;
        actor.character.poses = [new() { id = "invalid", parts = [new() { id = "face", texture = 1 }, new() { id = "missing", texture = 0 }] }];
        bool rejected = false;
        try { actor.ApplyAppearance("invalid", ""); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && image.texture == still && faceObject.transform.localPosition == new Vector3(200, 0, 0),
            "Invalid native appearance partially changed existing parts.");
        actor.gameObject.SetActive(false);
        Check(NovelSceneCapture.Capture(scene, view).items.Count == 0, "Inactive native character retained a visible private part.");
        actor.gameObject.SetActive(true); scene.DestroyObject(faceObject);
        Check(NovelSceneCapture.Capture(scene, view).items.Count == 0, "Deleting a native character part left a rendered ghost.");
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}

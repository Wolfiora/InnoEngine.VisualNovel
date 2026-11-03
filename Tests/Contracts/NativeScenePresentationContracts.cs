using System;
using System.Linq;
using Inno.Core.Identity;
using Inno.Core.Mathematics;
using Inno.Core.Serialization;
using Inno.Extensibility.Types;
using Inno.Rendering;
using Inno.Rendering.Assets;
using Inno.Scene;
using Inno.Assets;
using Inno.VisualNovel.Integration.Components;
using Inno.VisualNovel.Integration.Hosting;
using Inno.VisualNovel.Integration.Rendering;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class NativeScenePresentationContracts
{
    internal static void ObjectsAreTheOnlyPresentationAuthority(
        IdentityAllocator identities,
        TypeCatalog types,
        SerializationRegistry native
    ) {
        using var world = new SceneWorld(identities, types);
        using var scope = world.EnterScope();
        var scene = SceneManager.LoadNewSceneAdditive("Authoring");
        var system = scene.AddSystem<NovelSceneSystem>(); system.surfaceMaterial = new MaterialAsset();
        Check(!system.hasPresentation && system.project is null && system.CaptureScene().quads.Count == 0,
            "An empty native scene needed a running story or a holder object to draw.");
        var parent = scene.CreateObject("Group");
        var child = scene.CreateObject("Actual image"); child.transform.SetParent(parent.transform);
        var image = child.AddComponent<NovelImageComponent>(); image.width = 100; image.height = 80;
        var view = new NovelSceneProjection(800, 600, Matrix.identity);
        var first = NovelSceneCapture.Capture(scene, view);
        Check(first.items.Count == 1 && first.items[0].topLeft == new System.Numerics.Vector2(350, 260)
            && NovelSceneCapture.HitTest(scene, view, new(400, 300)) == child, "Native geometry did not drive drawing and selection.");
        image.crop = new(.25f, .25f, .5f, .5f); image.uv = new(.5f, 0, .5f, 1);
        var cropped = NovelSceneCapture.Capture(scene, view).items.Single();
        Check(cropped.topLeft == new System.Numerics.Vector2(375, 280)
            && NovelSceneCapture.HitTest(scene, view, new(360, 270)) is null
            && NovelSceneCapture.HitTest(scene, view, new(400, 300)) == child,
            "Native cropping did not remove the same geometry from painting and picking.");
        image.crop = new(0, 0, 1, 1); image.uv = new(0, 0, 1, 1);
        var context = AssetSerializationContext.Create(new FrameAssetResolver(system.surfaceMaterial!));
        byte[] before = native.Serialize(scene, context);
        _ = system.CaptureScene(); _ = system.CaptureScene();
        Check(native.Serialize(scene, context).SequenceEqual(before) && !system.hasPresentation,
            "Passive rendering changed authored state or started a live session.");
        parent.transform.localPosition = new(120, 30, 0);
        var moved = NovelSceneCapture.Capture(scene, view).items.Single();
        Check(moved.topLeft == new System.Numerics.Vector2(470, 230)
            && NovelSceneCapture.HitTest(scene, view, new(400, 300)) is null
            && NovelSceneCapture.HitTest(scene, view, new(520, 270)) == child, "Changing a parent transform did not affect actual presentation.");
        parent.SetActive(false);
        Check(NovelSceneCapture.Capture(scene, view).items.Count == 0 && NovelSceneCapture.HitTest(scene, view, new(520, 270)) is null,
            "An inactive native ancestor left a visible or selectable ghost.");
        parent.SetActive(true); image.visible = false;
        Check(NovelSceneCapture.Capture(scene, view).items.Count == 0, "The image's component visibility was ignored.");
        image.visible = true; parent.transform.localScale = new(-2, 1, 1);
        Check(NovelSceneCapture.HitTest(scene, view, new(520, 270)) == child, "Reflected native transforms broke hit testing.");
        parent.transform.localScale = Vector3.ZERO;
        Check(NovelSceneCapture.HitTest(scene, view, new(520, 270)) is null, "A degenerate quad captured input.");
        parent.transform.localScale = Vector3.ONE;
        var target = child.AddComponent<NovelTargetComponent>(); target.targetId = "hero";
        Check(NovelSceneCapture.Targets(scene)["hero"] == child, "Story target resolution ignored the native object.");
        var duplicate = scene.CreateObject("Duplicate"); duplicate.AddComponent<NovelTargetComponent>().targetId = "hero";
        bool rejected = false;
        try { _ = NovelSceneCapture.Targets(scene); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Duplicate target identity was silently resolved by order.");
        scene.DestroyObject(duplicate); scene.DestroyObject(child);
        Check(NovelSceneCapture.Capture(scene, view).items.Count == 0 && NovelSceneCapture.Targets(scene).Count == 0,
            "Deleting a native object left a second live presentation tree.");
    }
    internal static void PrefabInstancesAndNativeRoundTrip(
        IdentityAllocator identities,
        TypeCatalog types,
        SerializationRegistry native
    ) {
        using var world = new SceneWorld(identities, types);
        using var scope = world.EnterScope();
        var source = SceneManager.LoadNewSceneAdditive("Prefab source");
        var root = source.CreateObject("Actor");
        var part = source.CreateObject("Portrait"); part.transform.SetParent(root.transform);
        part.transform.localPosition = new(32, 24, 0);
        var image = part.AddComponent<NovelImageComponent>(); image.width = 100; image.height = 160;
        image.crop = new(.1f, .2f, .8f, .6f); image.color = 0xABCDEF80;
        var resolver = new FrameAssetResolver(new MaterialAsset());
        var prefab = PrefabAsset.Capture(root, native, resolver);
        var content = prefab.CaptureContent();
        var reloaded = PrefabAsset.CreateImported(content.GetPayload(), content.GetDependencies());
        resolver.Add(reloaded);
        var destination = SceneManager.LoadNewSceneAdditive("Instances");
        var a = reloaded.Instantiate(destination, native, resolver);
        var b = reloaded.Instantiate(destination, native, resolver);
        var ai = a.transform.children.Single().gameObject.GetComponent<NovelImageComponent>()!;
        var bi = b.transform.children.Single().gameObject.GetComponent<NovelImageComponent>()!;
        Check(a.identity.persistentId != b.identity.persistentId && ai.identity.persistentId != bi.identity.persistentId
            && ai.identity.persistentId != image.identity.persistentId,
            "Native Prefab instances reused object or component identities.");
        ai.width = 240; ai.transform.localPosition = new(100, 0, 0);
        Check(bi.width == 100 && image.width == 100 && bi.transform.localPosition == new Vector3(32, 24, 0),
            "Editing a native Prefab instance mutated its source or another instance.");
        var view = new NovelSceneProjection(800, 600, Matrix.identity);
        Check(NovelSceneCapture.Capture(destination, view).items.Count == 2,
            "Real Prefab child components were not captured.");
        a.SetActive(false);
        Check(NovelSceneCapture.Capture(destination, view).items.Count == 1,
            "Disabling a Prefab root left its native child visible.");
        byte[] sceneBytes = native.Serialize(destination, AssetSerializationContext.Create(resolver));
        SceneManager.UnloadScene(destination);
        var reopened = native.Deserialize<GameScene>(sceneBytes, AssetSerializationContext.Create(resolver));
        var restored = reopened.GetObjects().SelectMany(item => item.GetComponents<NovelImageComponent>()).ToArray();
        Check(restored.Length == 2 && restored.Any(item => item.width == 240) && restored.Any(item => item.width == 100)
            && restored.All(item => item.color == 0xABCDEF80 && item.crop == new Rect(.1f, .2f, .8f, .6f)),
            "Native scene persistence lost independently authored component values.");
    }
    internal static void OutputAndCameraOwnership(
        IdentityAllocator identities,
        TypeCatalog types
    ) {
        using var world = new SceneWorld(identities, types);
        using var scope = world.EnterScope();
        var first = SceneManager.LoadNewSceneAdditive("First"); var a = first.AddSystem<NovelSceneSystem>();
        var second = SceneManager.LoadNewSceneAdditive("Second"); var b = second.AddSystem<NovelSceneSystem>();
        Check(NovelRenderModel.SelectSystem([first, second], first.identity.persistentId) == a
            && NovelRenderModel.SelectSystem([first, second], second.identity.persistentId) == b, "Game output ignored the explicit content root.");
        bool ambiguous = false;
        try { _ = NovelRenderModel.SelectSystem([first, second], null); } catch (InvalidOperationException) { ambiguous = true; }
        Check(ambiguous, "An ambiguous game output guessed by enumeration order.");
        b.gameOutput = false;
        Check(NovelRenderModel.SelectSystem([first, second], null) == a
            && NovelRenderModel.SelectSystem([first, second], second.identity.persistentId) is null,
            "A non-output active root caused an implicit switch to an unrelated scene.");
        var camera = first.CreateObject("Camera").AddComponent<NovelCameraComponent>();
        camera.transform.localPosition = new(100, 50, 0); camera.zoom = 2;
        var view = NovelSceneProjection.Game(800, 600, camera);
        Check(view.Project(new(100, 50, 0)) == new System.Numerics.Vector2(400, 300)
            && view.Project(new(150, 100, 0)) == new System.Numerics.Vector2(500, 200), "Game camera projection differs from native transforms.");
        var target = first.CreateObject("Follow target"); target.transform.localPosition = new(400, 200, 0);
        var cameraParent = first.CreateObject("Camera rig"); cameraParent.transform.localPosition = new(10, 20, 0);
        cameraParent.transform.localScale = new(2, 2, 1); camera.transform.SetParent(cameraParent.transform);
        camera.followTarget = target; camera.followOffset = new(40, -20, 0); camera.following = true;
        Vector3 authored = camera.transform.localPosition;
        _ = NovelSceneProjection.Game(800, 600, camera);
        Check(camera.transform.localPosition == authored, "Capturing a follow camera changed the authored Transform.");
        NovelSceneSimulation.Tick(first, .1);
        Check(camera.transform.worldPosition == new Vector3(440, 180, 0), "Camera follow ignored the native parent transform or target position.");
        var performance = first.CreateObject("Target motion").AddComponent<NovelPerformanceComponent>();
        performance.clip = new Model.Performance.PerformanceClipAsset { duration = 1, tracks = [new()
            { target = "actor", property = Model.Performance.PerformanceProperty.X, keys = [new() { value = 600 }] }] };
        performance.bindings = [new() { slot = "actor", target = target }]; performance.playing = true;
        NovelSceneSimulation.Tick(first, .1);
        Check(camera.transform.worldPosition.x == 640, "Camera follow ran before a later sibling's animation update.");
        target.SetActive(false); target.transform.localPosition = new(900, 0, 0); NovelSceneSimulation.Tick(first, .1);
        Check(camera.transform.worldPosition.x == 640, "An inactive follow target moved the camera.");
        target.SetActive(true); performance.Stop();
        performance.bindings = [new() { slot = "actor", target = camera.gameObject }];
        bool conflict = false;
        try { performance.Play(); } catch (InvalidOperationException) { conflict = true; }
        Check(conflict && camera.transform.worldPosition.x == 640 && !performance.playing, "A position clip competed with the native follow writer.");
        camera.following = false; camera.shakeOffset = new(5, -3);
        var shaken = NovelSceneProjection.Game(800, 600, camera).Project(camera.transform.worldPosition);
        Check(shaken == new System.Numerics.Vector2(390, 294) && camera.transform.worldPosition.x == 640,
            "Camera shake changed the authored position or used the wrong camera-local channel.");
        camera.followTarget = camera.gameObject; camera.following = true;
        bool cycle = false;
        try { NovelSceneSimulation.Tick(first, .1); } catch (InvalidOperationException) { cycle = true; }
        Check(cycle, "Self-follow introduced frame-dependent transform drift.");
        camera.followTarget = target; first.DestroyObject(target);
        bool missing = false;
        try { camera.ApplyFollow(); } catch (InvalidOperationException) { missing = true; }
        Check(missing, "A destroyed follow target was implicitly replaced or dereferenced.");
    }
    private sealed class FrameAssetResolver(params AssetObject[] initial) : IAssetReferenceResolver
    {
        private readonly System.Collections.Generic.List<AssetObject> m_assets = [.. initial];
        internal void Add(AssetObject asset) => m_assets.Add(asset);
        public AssetObject Resolve(
            Guid persistentId,
            Guid stableTypeId,
            string lastKnownPath,
            Type expectedType,
            string propertyPath
        ) => m_assets.SingleOrDefault(asset => asset.identity.persistentId == persistentId) ?? throw new InvalidOperationException("Unexpected test asset reference: " + persistentId);
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}

using System;
using System.Linq;
using Inno.Core.Identity;
using Inno.Core.Mathematics;
using Inno.Extensibility.Types;
using Inno.Scene;
using Inno.VisualNovel.Integration.Components;
using Inno.VisualNovel.Integration.Rendering;
using Inno.VisualNovel.Model.Performance;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class NativeParallaxContracts
{
    internal static void CameraTranslationPreservesNativeOwnership(
        IdentityAllocator identities,
        TypeCatalog types
    ) {
        using var world = new SceneWorld(identities, types); using var scope = world.EnterScope();
        var scene = SceneManager.LoadNewSceneAdditive("Parallax");
        var parent = scene.CreateObject("Camera parent"); parent.transform.localPosition = new(30, 10, 0);
        var camera = scene.CreateObject("Camera").AddComponent<NovelCameraComponent>(); camera.transform.SetParent(parent.transform);
        camera.transform.localPosition = new(10, 0, 0);
        var image = scene.CreateObject("Background").AddComponent<NovelImageComponent>(); image.width = image.height = 100;
        var game = NovelSceneProjection.Game(800, 600, camera);
        var editor = new NovelSceneProjection(800, 600, camera.transform.worldToLocalMatrix);
        image.parallax = 0;
        Check(NovelSceneCapture.Capture(scene, game).items.Single().topLeft == new System.Numerics.Vector2(350, 250), "A fixed image followed camera translation.");
        image.parallax = .5f;
        var half = NovelSceneCapture.Capture(scene, game).items.Single();
        Check(half.topLeft == new System.Numerics.Vector2(330, 255)
            && ReferenceEquals(NovelSceneCapture.HitTest(scene, game, new(350, 270)), image.gameObject), "Parallax painting and selection disagreed.");
        image.parallax = 1;
        var ordinary = NovelSceneCapture.Capture(scene, game).items.Single();
        image.parallax = 0;
        Check(NovelSceneCapture.Capture(scene, editor).items.Single().topLeft == ordinary.topLeft
            && image.transform.localPosition == Vector3.ZERO, "Game parallax changed authored transforms or editor navigation.");
        var emitter = scene.CreateObject("Fixed particles").AddComponent<NovelParticleComponent>();
        emitter.preset = new VisualEffectAsset { resourceId = "particles", kind = VisualEffectKind.Particles,
            particles = new ParticlePreset { rate = 4, minimumLifetime = 10, maximumLifetime = 10, parallax = 0 } };
        emitter.elapsed = 1;
        var baseline = NovelSceneCapture.Capture(scene, new(800, 600, Matrix.identity)).items.Where(item => item.owner == emitter.gameObject.identity.persistentId.ToString()).ToArray();
        var fixedItems = NovelSceneCapture.Capture(scene, game).items.Where(item => item.owner == emitter.gameObject.identity.persistentId.ToString()).ToArray();
        Check(baseline.Length > 0 && baseline.Select(item => item.topLeft).SequenceEqual(fixedItems.Select(item => item.topLeft)), "Particle preset parallax was ignored by native capture.");
        camera.transform.localRotation = Quaternion.FromEulerAnglesXYZDegrees(new(0, 0, 90)); camera.zoom = 2;
        var rotated = NovelSceneProjection.Game(800, 600, camera);
        var expected = new NovelSceneProjection(800, 600, Matrix.CreateRotationZ(-MathF.PI / 2), 2).Project(new(20, 10, 0));
        Check(System.Numerics.Vector2.Distance(rotated.Project(new(20, 10, 0), 0), expected) < .001f,
            "Translation parallax incorrectly removed camera rotation or zoom.");
        bool rejected = false;
        try { _ = rotated.Project(Vector3.ZERO, double.NaN); } catch (ArgumentOutOfRangeException) { rejected = true; }
        Check(rejected, "Nonfinite parallax reached geometry output.");
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}

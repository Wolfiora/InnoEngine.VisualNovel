using System;
using System.Linq;
using Inno.Assets;
using Inno.Assets.Pipeline;
using Inno.Core.Mathematics;
using Inno.Core.Serialization;
using Inno.Editor.Inspection;
using Inno.Editor.Interactions;
using Inno.Editor.Scene;
using Inno.Scene;
using Inno.VisualNovel.Authoring.SceneEditing;
using Inno.VisualNovel.Integration.Components;
using Inno.VisualNovel.Integration.Hosting;
using Inno.VisualNovel.Integration.Rendering;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Project;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class ConsumerStageWorkshop
{
    internal const string C_SCENE = "Game/Scenes/NativeStageWorkshop.iscene";

    internal static void Create(
        SceneEdits scenes,
        IInspectionPropertyEditService properties,
        IEditorHistory history,
        NovelProjectAsset game,
        SerializationRegistry serialization,
        AssetPipeline assets
    ) {
        var edits = new NovelSceneEditService(scenes, properties);
        var scene = edits.CreateGameScene(game);
        scenes.RenameScene(scene, "Native Stage Workshop");
        var system = scene.GetSystems().OfType<NovelSceneSystem>().Single();
        system.designWidth = game.width; system.designHeight = game.height;
        system.project = null;
        var actors = new NovelCharacterEditService(scenes, history, properties);
        var featured = game.characters.Take(2).Select((resource, index) =>
        {
            var actor = actors.Create(scene, resource);
            actor.gameObject.name = "Actor " + (index + 1) + " - " + resource.displayName;
            actor.gameObject.AddComponent<NovelTargetComponent>().targetId = "workshop/actor-" + (index + 1);
            actor.playing = false; actor.elapsed = .4;
            var parts = actor.Parts().ToArray();
            float tallest = parts.Select(static part => part.gameObject.GetComponent<NovelImageComponent>().height).DefaultIfEmpty(800).Max();
            float scale = Math.Min(1, game.height * .72f / tallest);
            actor.transform.localScale = new(scale, scale, 1);
            actor.transform.localPosition = new((index == 0 ? -.34f : .05f) * game.width, game.height * .30f, 0);
            var prefab = PrefabAsset.Capture(actor.gameObject, serialization, assets);
            Save("Game/Prefabs/WorkshopActor" + (index + 1) + ".iprefab", prefab);
            return actor;
        }).ToArray();
        var grade = new VisualEffectAsset { resourceId = "workshop/grade", displayName = "Workshop color grade", kind = VisualEffectKind.Screen,
            screen = new() { kind = ScreenEffectKind.ColorGrade, saturation = .95, contrast = 1.05 } };
        Save("Game/Workshop/Grade.ivneffect", grade);
        var effect = edits.CreateScreenEffect(scene); effect.gameObject.name = "Color Grade - instance parameters";
        effect.preset = assets.Load<VisualEffectAsset>(AssetPath.Project("Game/Workshop/Grade.ivneffect"));
        effect.strength = .35;
        var dust = new VisualEffectAsset { resourceId = "workshop/dust", displayName = "Workshop drifting light", kind = VisualEffectKind.Particles,
            particles = new() { width = game.width, height = game.height, rate = 7, minimumLifetime = 4, maximumLifetime = 6,
                minimumVelocityX = -8, maximumVelocityX = 8, minimumVelocityY = -16, maximumVelocityY = -6,
                startSize = 4, endSize = 1, startColor = 0xCFE7FF66, endColor = 0xCFE7FF00 } };
        Save("Game/Workshop/Dust.ivneffect", dust);
        var particles = edits.CreateParticles(scene); particles.gameObject.name = "Drifting lights - scrub Elapsed";
        particles.preset = assets.Load<VisualEffectAsset>(AssetPath.Project("Game/Workshop/Dust.ivneffect"));
        particles.transform.localPosition = new(-game.width / 2, game.height / 2, 0); particles.elapsed = 3; particles.playing = false; particles.seed = 304;
        var audio = edits.CreateAudio(scene); audio.gameObject.name = "Audio - playback disabled in this workshop"; audio.playOnEnable = false;
        audio.cue = game.audio.FirstOrDefault(static cue => cue.channel is Model.Audio.NovelAudioChannel.Ambience or Model.Audio.NovelAudioChannel.Bgm)
            ?? game.audio.FirstOrDefault();
        audio.gain = .35;
        var clip = new PerformanceClipAsset { resourceId = "workshop/native-motion", displayName = "Native object motion and grading", duration = 2,
            tracks = [
                new() { target = "camera", property = PerformanceProperty.ShakeX, keys = [new() { value = 0 }, new() { time = 1, value = 4 }, new() { time = 2, value = 0 }] },
                new() { target = "grade", property = PerformanceProperty.Saturation, keys = [new() { value = .75 }, new() { time = 2, value = 1.15 }] },
                new() { target = "audio", property = PerformanceProperty.Volume, keys = [new() { value = .25 }, new() { time = 2, value = .5 }] }
            ], markers = [new() { id = "midpoint", name = "Composition midpoint", time = 1 }] };
        Save("Game/Workshop/Motion.ivnclip", clip);
        var performance = edits.CreatePerformance(scene); performance.gameObject.name = "Performance - Apply current position in Inspector";
        performance.clip = assets.Load<PerformanceClipAsset>(AssetPath.Project("Game/Workshop/Motion.ivnclip"));
        performance.bindings = [new() { slot = "camera", target = system.camera!.gameObject }, new() { slot = "grade", target = effect.gameObject }, new() { slot = "audio", target = audio.gameObject }];
        new NovelPerformanceEditService(history, properties).Seek(performance, .75);
        if (featured.Length > 0) { system.camera.followTarget = featured[0].gameObject; system.camera.following = false; }
        Save(C_SCENE, SceneAsset.Capture(scene, serialization, assets));
        Validate(scene);
        Console.WriteLine("Saved native stage workshop and " + featured.Length + " reusable actor Prefabs; source resources remain unchanged.");

        void Save(string path, AssetObject value)
        {
            if (!EditorAssets.Save(AssetPath.Project(path), value)) throw new InvalidOperationException("Workshop asset could not save: " + path);
        }
    }

    internal static void Validate(GameScene scene)
    {
        var system = scene.GetSystems().OfType<NovelSceneSystem>().Single();
        Check(system.project is null && system.camera is not null, "The stage workshop must be a passive scene with an explicit camera.");
        var view = NovelSceneProjection.Game(system.width, system.height, system.camera);
        var first = NovelSceneCapture.Capture(scene, view);
        var particles = scene.GetObjects().SelectMany(static owner => owner.GetComponents<NovelParticleComponent>()).Single();
        var performance = scene.GetObjects().SelectMany(static owner => owner.GetComponents<NovelPerformanceComponent>()).Single();
        var audio = scene.GetObjects().SelectMany(static owner => owner.GetComponents<NovelAudioComponent>()).Single();
        Check(first.items.Count > 0 && first.effects?.Count == 1 && !audio.playOnEnable && !audio.isPlaying && !performance.playing,
            "The native stage workshop lacks actual draw/effect data or starts background playback.");
        var again = NovelSceneCapture.Capture(scene, view);
        Check(again.items.SequenceEqual(first.items) && again.effects!.SequenceEqual(first.effects!) && performance.elapsed == .75 && particles.elapsed == 3,
            "Repeated passive stage capture advanced or changed authored state.");
        int count = first.items.Count;
        particles.gameObject.SetActive(false);
        Check(NovelSceneCapture.Capture(scene, view).items.Count < count, "Disabling the emitter left its particle geometry in the native stage.");
        particles.gameObject.SetActive(true);
        var effect = scene.GetObjects().SelectMany(static owner => owner.GetComponents<NovelScreenEffectComponent>()).Single();
        double inherited = effect.preset!.screen.saturation;
        performance.Seek(2);
        Check(effect.parameters?.saturation == 1.15 && effect.preset.screen.saturation == inherited && audio.gain == .5 && !audio.isPlaying,
            "Native timeline sampling did not isolate effect parameters and silent scene audio gain.");
        performance.Seek(.75);
        var actor = scene.GetObjects().SelectMany(static owner => owner.GetComponents<NovelCharacterComponent>()).FirstOrDefault();
        if (actor is not null)
        {
            Check(actor.Parts().All(part => ReferenceEquals(part.character, actor)), "An actor Prefab lost actual part ownership.");
            actor.gameObject.SetActive(false);
            Check(NovelSceneCapture.Capture(scene, view).items.Count < count, "Disabling the actor left native part geometry visible.");
            actor.gameObject.SetActive(true);
        }
        Console.WriteLine("Native stage: " + scene.GetObjects().Count + " objects, " + count + " draw items; component effects, emitter/actor activation, silent audio and timeline sampling passed. GPU untested.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

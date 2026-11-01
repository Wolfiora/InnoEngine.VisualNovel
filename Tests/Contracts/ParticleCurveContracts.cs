using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.TimelineEditing;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Stage.Composition;
using Inno.VisualNovel.Stage.Effects;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class ParticleCurveContracts
{
    internal static void NativeLifetimeAndOwnership(SerializationRegistry native)
    {
        var asset = ParticleContracts.Preset(); var preset = asset.particles;
        preset.rate = 1; preset.minimumLifetime = preset.maximumLifetime = 1;
        preset.width = preset.height = 0; preset.minimumVelocityX = preset.maximumVelocityX = preset.minimumVelocityY = preset.maximumVelocityY = 0;
        ParticleCurveEditService.SetSize(preset, -1, new() { time = 0, value = 10, interpolation = PerformanceInterpolation.EaseIn });
        ParticleCurveEditService.SetSize(preset, -1, new() { time = .5, value = 30 });
        ParticleCurveEditService.SetSize(preset, -1, new() { time = 1, value = 0 });
        ParticleCurveEditService.SetColor(preset, -1, new() { progress = 0, color = 0x00000000 });
        ParticleCurveEditService.SetColor(preset, -1, new() { progress = .5, color = 0xFFFFFFFF });
        ParticleCurveEditService.SetColor(preset, -1, new() { progress = 1, color = 0xFFFFFF00 });
        var restored = native.Deserialize<ParticlePreset>(native.Serialize(preset)); asset.particles = restored;
        var simulator = new ParticleSimulator(asset);
        var state = new ParticleEmitterState { id = "one", presetId = asset.resourceId, seed = 17, elapsed = .75 };
        var early = simulator.Evaluate(state).Single();
        Check(Math.Abs(early.height - 15) < 1e-9 && early.color == 0x80808080, "Native lifetime keys ignored eased height or the actual RGBA gradient.");
        state.elapsed = 1.25; var late = simulator.Evaluate(state).Single();
        Check(Math.Abs(late.height - 15) < 1e-9 && late.color == 0xFFFFFF80, "Particle age was evaluated as emission time or failed the second gradient segment.");
        restored.sizeKeys[0].value = 900; restored.colorKeys[0].color = 0xFF0000FF;
        state.elapsed = .75; Check(simulator.Evaluate(state).Single() == early, "The running emitter retained mutable authoring keys.");
        bool rejected = false; int count = preset.sizeKeys.Length;
        try { ParticleCurveEditService.SetSize(preset, -1, new() { time = .5, value = 3 }); } catch (ArgumentException) { rejected = true; }
        Check(rejected && preset.sizeKeys.Length == count && preset.sizeKeys[1].value == 30, "A colliding lifetime key partially changed the curve.");
        restored = native.Deserialize<ParticlePreset>(native.Serialize(preset)); asset.particles = restored;
        restored.colorKeys[0].interpolation = PerformanceInterpolation.Hold;
        Check(new ParticleSimulator(asset).Evaluate(state).Single().color == 0, "Hold interpolation advanced before its next color stop.");
        var stage = new StageController([], particlePresets: [asset]); stage.particles.Start("one", asset.resourceId, 17); stage.Tick(.75);
        var snapshot = native.Deserialize<StageState>(native.Serialize(stage.Capture())); var frame = stage.BuildFrame(1920, 1080).items.ToArray();
        stage.Tick(.5); stage.Restore(snapshot);
        Check(stage.BuildFrame(1920, 1080).items.SequenceEqual(frame), "Restoring seeded logical time changed lifetime curves.");
        restored.sizeKeys[1].time = -1; rejected = false;
        try { _ = new ParticleSimulator(asset); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "An invalid normalized particle age reached playback.");
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}

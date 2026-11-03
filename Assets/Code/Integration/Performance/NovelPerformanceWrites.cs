using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Mathematics;
using InnoEngine.Scene;
using Inno.VisualNovel.Integration.Components;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Stage.Timeline;
using Inno.VisualNovel.Stage.Effects;

namespace Inno.VisualNovel.Integration.Performance;

internal static class NovelPerformanceWrites
{
    internal readonly record struct Write(GameObject target, PerformanceProperty channel, EngineObject owner, string property, Action apply);

    internal static Write[] Prepare(
        NovelPerformanceComponent performance,
        double seconds
    ) {
        var clip = performance.clip ?? throw new InvalidOperationException("Assign a performance clip before applying its tracks.");
        var bindings = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        foreach (var binding in performance.bindings)
        {
            if (string.IsNullOrWhiteSpace(binding.slot) || binding.target is not { isDestroyed: false } target
                || !ReferenceEquals(target.scene, performance.gameObject.scene))
                throw new InvalidOperationException("A performance binding requires a slot and a live target in the same scene: " + binding.slot);
            if (!bindings.TryAdd(binding.slot, target)) throw new InvalidOperationException("Duplicate performance slot: " + binding.slot);
        }
        var channels = new HashSet<(GameObject, PerformanceProperty)>();
        return new PerformancePlayer(clip).Evaluate(seconds).Select(sample =>
        {
            if (!bindings.TryGetValue(sample.target, out var target)) throw new InvalidOperationException("Unbound performance slot: " + sample.target);
            if (!channels.Add((target, sample.property))) throw new InvalidOperationException("Two clip slots write the same native object channel: " + sample.target + "/" + sample.property);
            return Prepare(target, sample);
        }).ToArray();
    }

    private static Write Prepare(
        GameObject target,
        PerformanceSample sample
    ) {
        Transform transform = target.transform;
        float value = (float)sample.value;
        if (sample.property is PerformanceProperty.X or PerformanceProperty.Y
            && target.GetComponents<NovelCameraComponent>().Any(static camera => camera.following))
            throw new InvalidOperationException("Position tracks conflict with camera following. Stop following before animating the camera Transform: " + sample.target);
        return sample.property switch
        {
            PerformanceProperty.X => new(target, sample.property, transform, nameof(transform.localPosition),
                () => transform.localPosition = new(value, transform.localPosition.y, transform.localPosition.z)),
            PerformanceProperty.Y => new(target, sample.property, transform, nameof(transform.localPosition),
                () => transform.localPosition = new(transform.localPosition.x, value, transform.localPosition.z)),
            PerformanceProperty.ScaleX => new(target, sample.property, transform, nameof(transform.localScale),
                () => transform.localScale = new(value, transform.localScale.y, transform.localScale.z)),
            PerformanceProperty.ScaleY => new(target, sample.property, transform, nameof(transform.localScale),
                () => transform.localScale = new(transform.localScale.x, value, transform.localScale.z)),
            PerformanceProperty.Rotation => new(target, sample.property, transform, nameof(transform.localRotation),
                () => transform.localRotation = Quaternion.FromEulerAnglesXYZDegrees(new(0, 0, value))),
            PerformanceProperty.Opacity => Opacity(),
            PerformanceProperty.EffectStrength => EffectStrength(),
            PerformanceProperty.Volume => Volume(),
            PerformanceProperty.ShakeX or PerformanceProperty.ShakeY => CameraShake(),
            PerformanceProperty.BlurRadius or PerformanceProperty.VignetteRadius or PerformanceProperty.VignetteSoftness
                or PerformanceProperty.Saturation or PerformanceProperty.Brightness or PerformanceProperty.Contrast
                or PerformanceProperty.PixelSize or PerformanceProperty.LetterboxRatio or PerformanceProperty.AberrationPixels => EffectParameter(),
            _ => throw new InvalidOperationException("The native target does not expose this performance channel: " + sample.target + "/" + sample.property)
        };

        Write Opacity()
        {
            target.TryGetComponent<NovelImageComponent>(out var image);
            target.TryGetComponent<Components.Interface.NovelUiLayoutComponent>(out var layout);
            if (image is not null && layout is not null)
                throw new InvalidOperationException("Opacity is ambiguous on an object with both a stage image and UI layout: " + sample.target);
            if (layout is not null)
                return new(target, sample.property, layout, nameof(layout.opacity), () => layout.opacity = value);
            if (image is null) throw new InvalidOperationException("Opacity requires a VN Image or UI Layout component: " + sample.target);
            return new(target, sample.property, image, nameof(image.color), () => image.color = (image.color & 0xFFFFFF00) | (uint)Math.Round(value * 255));
        }
        Write EffectStrength()
        {
            if (!target.TryGetComponent<NovelScreenEffectComponent>(out var effect) || effect is null || effect.duration != 0)
                throw new InvalidOperationException("EffectStrength requires a VN Screen Effect with its independent fade disabled: " + sample.target);
            return new(target, sample.property, effect, nameof(effect.strength), () => effect.strength = value);
        }
        Write Volume()
        {
            if (!target.TryGetComponent<NovelAudioComponent>(out var audio) || audio is null)
                throw new InvalidOperationException("Volume requires a VN Audio component: " + sample.target);
            return new(target, sample.property, audio, nameof(audio.gain), () => audio.gain = value);
        }
        Write EffectParameter()
        {
            if (!target.TryGetComponent<NovelScreenEffectComponent>(out var effect) || effect is null)
                throw new InvalidOperationException("This parameter requires a VN Screen Effect component: " + sample.target);
            var channel = new Dictionary<(string target, PerformanceProperty property), double> { [(sample.target, sample.property)] = sample.value };
            _ = ScreenEffectController.Parameters(sample.target, effect.CaptureParameters(), channel, out _);
            return new(target, sample.property, effect, nameof(effect.parameters),
                () => effect.parameters = ScreenEffectController.Parameters(sample.target, effect.CaptureParameters(), channel, out _));
        }
        Write CameraShake()
        {
            if (!target.TryGetComponent<NovelCameraComponent>(out var camera) || camera is null)
                throw new InvalidOperationException("Native shake channels require a VN Camera component: " + sample.target);
            return new(target, sample.property, camera, nameof(camera.shakeOffset), () => camera.shakeOffset = sample.property == PerformanceProperty.ShakeX
                ? new(value, camera.shakeOffset.y) : new(camera.shakeOffset.x, value));
        }
    }
}

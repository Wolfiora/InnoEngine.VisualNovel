using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Mathematics;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Stage.Composition;

namespace Inno.VisualNovel.Stage.Camera;

internal static class StageCameraEvaluator
{
    internal static void Validate(StageState state)
    {
        if (state.cameraFollow is not { } follow) return;
        var target = state.layers.FirstOrDefault(layer => layer.owner == follow.owner);
        if (!StageCameraTargetRules.IsValid(follow.owner) || target is null || Math.Abs(target.parallax) < .000001
            || state.layers.Any(layer => layer.owner == follow.owner && (layer.pivotX != target.pivotX || layer.pivotY != target.pivotY || layer.parallax != target.parallax))
            || !double.IsFinite(follow.anchorX) || !double.IsFinite(follow.anchorY) || follow.anchorX is < 0 or > 1 || follow.anchorY is < 0 or > 1
            || !double.IsFinite(follow.width) || !double.IsFinite(follow.height) || follow.width is <= 0 or > 1e7 || follow.height is <= 0 or > 1e7)
            throw new InvalidOperationException("Camera following requires an existing owner, normalized focal anchor and finite positive design canvas.");
    }
    internal static (double x, double y, double zoom, double rotation) Evaluate(
        StageState state,
        IReadOnlyDictionary<(string target, PerformanceProperty property), double> samples,
        double width,
        double height
    ) {
        double x = state.cameraX + Offset("camera", PerformanceProperty.X, PerformanceProperty.ShakeX);
        double y = state.cameraY + Offset("camera", PerformanceProperty.Y, PerformanceProperty.ShakeY);
        double zoom = state.cameraZoom * Value("camera", PerformanceProperty.ScaleX, 1);
        double rotation = state.cameraRotation + Value("camera", PerformanceProperty.Rotation, 0);
        if (state.cameraFollow is { } follow)
        {
            var target = state.layers.First(layer => layer.owner == follow.owner);
            var inverseView = Matrix.CreateRotationZ((float)(-rotation * Math.PI / 180)) * Matrix.CreateScale((float)(1 / zoom), (float)(1 / zoom), 1);
            Vector2 desired = Vector2.Transform(new((float)((follow.anchorX - .5) * width), (float)((follow.anchorY - .5) * height)), inverseView);
            x += (target.pivotX + Offset(follow.owner, PerformanceProperty.X, PerformanceProperty.ShakeX) - width / 2 - desired.x) / target.parallax;
            y += (target.pivotY + Offset(follow.owner, PerformanceProperty.Y, PerformanceProperty.ShakeY) - height / 2 - desired.y) / target.parallax;
        }
        return (x, y, zoom, rotation);
        double Value(
            string target,
            PerformanceProperty property,
            double fallback
        ) => samples.TryGetValue((target, property), out double value) ? value : fallback;
        double Offset(
            string target,
            PerformanceProperty position,
            PerformanceProperty shake
        ) => Value(target, position, 0) + Value(target, shake, 0);
    }
    internal static void StopFollowing(
        StageState state,
        IReadOnlyDictionary<(string target, PerformanceProperty property), double> samples
    ) {
        if (state.cameraFollow is not { } follow) return;
        var pose = Evaluate(state, samples, follow.width, follow.height);
        ApplyPose(state, pose, samples);
        state.cameraFollow = null;
    }
    internal static void ApplyPose(
        StageState state,
        (double x, double y, double zoom, double rotation) pose,
        IReadOnlyDictionary<(string target, PerformanceProperty property), double> samples
    ) {
        state.cameraX = pose.x - Value(PerformanceProperty.X) - Value(PerformanceProperty.ShakeX);
        state.cameraY = pose.y - Value(PerformanceProperty.Y) - Value(PerformanceProperty.ShakeY);
        state.cameraZoom = pose.zoom / samples.GetValueOrDefault(("camera", PerformanceProperty.ScaleX), 1);
        state.cameraRotation = pose.rotation - Value(PerformanceProperty.Rotation);
        double Value(PerformanceProperty property) => samples.GetValueOrDefault(("camera", property));
    }
}

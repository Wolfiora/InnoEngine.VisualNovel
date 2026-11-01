using System;
using Inno.Core.Mathematics;
using Inno.Rendering.Assets;
using Inno.VisualNovel.Authoring.StageEditing;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Stage.Characters;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class CharacterPlacementContracts
{
    internal static void ActualPoseBoundsAndRelativeAnchors()
    {
        var image = new TextureAsset(1, 1, TextureColorSpace.Srgb, "png");
        var actor = new CharacterAsset { textures = [image], poses = [new() { parts = [new() { id = "body", texture = 0, width = 120, height = 160,
            surface = new() { cropX = .25, cropWidth = .5 }, transform = new() { x = 20, y = 10 } }, new() { id = "hat", texture = 0, width = 80, height = 40,
                transform = new() { x = 40, y = -20 } }] }], expressions = [new() { parts = [new() { partId = "hat", visibility = 0 }] }], breathing = new() { enabled = true } };
        var initial = new StageTransform { x = 999, y = -123, pivotX = 30, pivotY = 20, scaleX = -2, scaleY = 1.5, rotation = 27, opacity = .6 };
        var reference = new Rect(32, 32, 736, 536);
        var original = CharacterCompositionBounds.Measure(actor, "", "", initial);
        var first = CharacterPlacementEditService.Place(actor, "", "", initial, reference, .25, 1);
        var bounds = CharacterCompositionBounds.Measure(actor, "", "", first);
        Check(Near(bounds.x + bounds.width / 2, 216) && Near(bounds.y + bounds.height, 568), "Left-bottom preset did not align actual cropped mirrored rotated pose bounds to the safe area.");
        Check(initial.x == 999 && initial.y == -123 && actor.breathing.enabled && first.scaleX == initial.scaleX && first.scaleY == initial.scaleY
            && first.pivotX == initial.pivotX && first.pivotY == initial.pivotY && first.rotation == initial.rotation && first.opacity == initial.opacity,
            "Placement mutated its source, animation policy or non-position transform fields.");
        var center = CharacterPlacementEditService.Place(actor, "", "", first, reference, .5, .5, .5, .5);
        bounds = CharacterCompositionBounds.Measure(actor, "", "", center);
        Check(Near(bounds.x + bounds.width / 2, 400) && Near(bounds.y + bounds.height / 2, 300), "Custom extent anchor did not use the explicit design reference.");
        var outside = CharacterPlacementEditService.Place(actor, "", "", center, reference, -1, .5, 1, .5);
        bounds = CharacterCompositionBounds.Measure(actor, "", "", outside);
        Check(Near(bounds.x + bounds.width, 32 - 736) && Near(bounds.width, original.width), "Offscreen placement changed pose geometry or clamped an explicit relative target.");
        foreach (var anchor in new[] { (0d, 0d), (.5, 0), (1, 0), (0, .5), (.5, .5), (1, .5), (0, 1), (.5, 1), (1, 1) })
        {
            var placed = CharacterPlacementEditService.Place(actor, "", "", initial, reference, .75, .5, anchor.Item1, anchor.Item2);
            var actual = CharacterCompositionBounds.Measure(actor, "", "", placed);
            Check(Near(actual.x + actual.width * anchor.Item1, 584) && Near(actual.y + actual.height * anchor.Item2, 300), "A nine-point alignment lost its measured anchor.");
        }
    }
    internal static void InvalidCandidatesPreserveDeclarations()
    {
        var image = new TextureAsset(1, 1, TextureColorSpace.Srgb, "png");
        var actor = new CharacterAsset { textures = [image], poses = [new() { parts = [new() { id = "body", texture = 0 }] }], expressions = [new()] };
        double originalWidth = actor.poses[0].parts[0].width;
        var transform = new StageTransform { x = 12, y = 34 };
        Reject(() => CharacterPlacementEditService.Place(actor, "", "", transform, new(0, 0, 100, 100), double.NaN, 1));
        Reject(() => CharacterPlacementEditService.Place(actor, "", "", transform, new(0, 0, 0, 100), .5, 1));
        Reject(() => CharacterPlacementEditService.Place(actor, "", "", transform, new(0, 0, 100, 100), .5, 1, 2, .5));
        Reject(() => CharacterCompositionBounds.Measure(actor, "missing", "", transform));
        actor.poses[0].parts[0].visible = false;
        Reject(() => CharacterCompositionBounds.Measure(actor, "", "", transform));
        Check(transform.x == 12 && transform.y == 34 && actor.poses[0].parts[0].width == originalWidth, "A rejected placement changed a caller-owned declaration.");
    }
    private static bool Near(
        double value,
        double expected
    ) => Math.Abs(value - expected) < .003;
    private static void Reject(Action action)
    { try { action(); } catch (Exception error) when (error is ArgumentException or InvalidOperationException) { return; } throw new InvalidOperationException("Invalid character placement was accepted."); }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}

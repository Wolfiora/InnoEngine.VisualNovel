using System;
using System.Globalization;
using System.Linq;
using InnoEditor.ImGui;
using Inno.VisualNovel.Authoring.CharacterEditing;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Model.Characters;

namespace Inno.VisualNovel.Editor.CharacterDesigner;

internal sealed class CharacterAnimationFields(StoryInspectorFields fields)
{
    internal bool Breathing(CharacterBreathing breathing)
    {
        if (!ImGui.CollapsingHeader("Character breathing")) return false;
        bool enabled = breathing.enabled; bool changed = fields.Toggle("character/breathing/enabled", "Enable subtle breathing", ref enabled); breathing.enabled = enabled;
        if (!enabled) return changed;
        double amplitude = breathing.amplitude, period = breathing.period, x = breathing.anchorX, y = breathing.anchorY;
        changed |= fields.Number("character/breathing/amplitude", "Relative amplitude", ref amplitude, 0, 0.1); breathing.amplitude = amplitude;
        changed |= fields.Number("character/breathing/period", "Full cycle (seconds)", ref period, 0.1, 3600); breathing.period = period;
        changed |= fields.Number("character/breathing/x", "Anchor X (0–1 within the pose)", ref x, 0, 1); breathing.anchorX = x;
        changed |= fields.Number("character/breathing/y", "Anchor Y (0–1 within the pose)", ref y, 0, 1); breathing.anchorY = y;
        EditorUiSession.current.widgets.Hint("Breathing uses a separate transform channel and preserves rotation, mirroring and performance keyframes. Reduced motion disables this optional movement.");
        return changed;
    }
    internal bool Part(
        CharacterAsset character,
        CharacterPart part,
        int? selectedImage
    ) {
        if (!ImGui.CollapsingHeader("Part animation")) return false;
        var animation = part.animation; string key = "character/animation/" + part.id; var mode = animation.mode;
        bool changed = fields.SelectEnum(key + "/mode", "Mechanism", ref mode);
        if (changed)
        {
            animation.mode = mode;
            if (mode != CharacterAnimationMode.None && animation.frames.Length == 0) CharacterAnimationEditService.AddFrame(character, part, part.texture);
        }
        if (mode == CharacterAnimationMode.None) return changed;
        bool loop = animation.loop, stagger = animation.stagger, essential = animation.essential;
        if (mode == CharacterAnimationMode.Sequence) { changed |= fields.Toggle(key + "/loop", "Loop sequence", ref loop); animation.loop = loop; }
        if (mode == CharacterAnimationMode.Blink)
        { double interval = animation.blinkInterval; changed |= fields.Number(key + "/interval", "Pause between blinks (seconds)", ref interval, 0, 3600); animation.blinkInterval = interval; }
        if (mode != CharacterAnimationMode.Talking) { changed |= fields.Toggle(key + "/stagger", "Offset the starting phase per instance", ref stagger); animation.stagger = stagger; }
        changed |= fields.Toggle(key + "/essential", "Keep frame animation with reduced motion", ref essential); animation.essential = essential;
        int remove = -1, moveFrom = -1, moveTo = -1;
        for (int index = 0; index < animation.frames.Length; index++)
        {
            var frame = animation.frames[index]; string prefix = key + "/frame/" + index; string texture = frame.texture.ToString(CultureInfo.InvariantCulture);
            if (fields.Select(prefix + "/image", "Frame " + (index + 1) + " image", ref texture, character.textures.Select((image, imageIndex) => (imageIndex.ToString(CultureInfo.InvariantCulture), image.name))))
            { frame.texture = int.Parse(texture, CultureInfo.InvariantCulture); changed = true; }
            double seconds = frame.seconds; changed |= fields.Number(prefix + "/duration", "Hold duration (seconds)", ref seconds, 0.001, 3600); frame.seconds = seconds;
            if (index > 0 && ImGui.SmallButton("Move up##" + prefix)) { moveFrom = index; moveTo = index - 1; }
            ImGui.SameLine(); if (index + 1 < animation.frames.Length && ImGui.SmallButton("Move down##" + prefix)) { moveFrom = index; moveTo = index + 1; }
            ImGui.SameLine(); if (ImGui.SmallButton("Remove frame##" + prefix)) remove = index;
        }
        if (moveFrom >= 0) { fields.Finish(); CharacterAnimationEditService.MoveFrame(animation, moveFrom, moveTo); changed = true; }
        if (remove >= 0) { fields.Finish(); CharacterAnimationEditService.RemoveFrame(animation, remove); changed = true; }
        ImGui.BeginDisabled(animation.frames.Length >= 256);
        try
        {
            if (ImGui.Button("Add current part image as a frame##" + key)) { fields.Finish(); CharacterAnimationEditService.AddFrame(character, part, part.texture); changed = true; }
            if (selectedImage is { } image && ImGui.Button("Add selected image as a frame##" + key)) { fields.Finish(); CharacterAnimationEditService.AddFrame(character, part, image); changed = true; }
        }
        finally { ImGui.EndDisabled(); }
        EditorUiSession.current.widgets.Hint("Sequences hold their final frame. Blinking and idle mouth animation use the current expression image as their resting frame. Mouth animation follows BeginSpeaking/EndSpeaking stage markers; it does not recognize phonemes. Removing the last frame disables part animation.");
        return changed;
    }
}

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
        if (!ImGui.CollapsingHeader(fields.Label("角色呼吸 / Breathing"))) return false;
        bool enabled = breathing.enabled; bool changed = fields.Toggle("character/breathing/enabled", "启用轻微呼吸", ref enabled); breathing.enabled = enabled;
        if (!enabled) return changed;
        double amplitude = breathing.amplitude, period = breathing.period, x = breathing.anchorX, y = breathing.anchorY;
        changed |= fields.Number("character/breathing/amplitude", fields.Localize("相对幅度"), ref amplitude, 0, 0.1); breathing.amplitude = amplitude;
        changed |= fields.Number("character/breathing/period", fields.Localize("完整周期（秒）"), ref period, 0.1, 3600); breathing.period = period;
        changed |= fields.Number("character/breathing/x", fields.Localize("锚点 X（姿势范围内 0–1）"), ref x, 0, 1); breathing.anchorX = x;
        changed |= fields.Number("character/breathing/y", fields.Localize("锚点 Y（姿势范围内 0–1）"), ref y, 0, 1); breathing.anchorY = y;
        EditorUiSession.current.widgets.Hint(fields.Localize("呼吸使用独立变换通道，保留角色旋转/镜像和演出关键帧。减少动态效果时关闭这一可选运动。"));
        return changed;
    }
    internal bool Part(
        CharacterAsset character,
        CharacterPart part,
        int? selectedImage
    ) {
        if (!ImGui.CollapsingHeader(fields.Label("部位动画 / Part animation"))) return false;
        var animation = part.animation; string key = "character/animation/" + part.id; var mode = animation.mode;
        bool changed = fields.SelectEnum(key + "/mode", "机制", ref mode);
        if (changed)
        {
            animation.mode = mode;
            if (mode != CharacterAnimationMode.None && animation.frames.Length == 0) CharacterAnimationEditService.AddFrame(character, part, part.texture);
        }
        if (mode == CharacterAnimationMode.None) return changed;
        bool loop = animation.loop, stagger = animation.stagger, essential = animation.essential;
        if (mode == CharacterAnimationMode.Sequence) { changed |= fields.Toggle(key + "/loop", "循环序列", ref loop); animation.loop = loop; }
        if (mode == CharacterAnimationMode.Blink)
        { double interval = animation.blinkInterval; changed |= fields.Number(key + "/interval", fields.Localize("眨眼间的停留秒数"), ref interval, 0, 3600); animation.blinkInterval = interval; }
        if (mode != CharacterAnimationMode.Talking) { changed |= fields.Toggle(key + "/stagger", "按实例错开起始相位", ref stagger); animation.stagger = stagger; }
        changed |= fields.Toggle(key + "/essential", "减少动态效果时仍保留帧动画", ref essential); animation.essential = essential;
        int remove = -1, moveFrom = -1, moveTo = -1;
        for (int index = 0; index < animation.frames.Length; index++)
        {
            var frame = animation.frames[index]; string prefix = key + "/frame/" + index; string texture = frame.texture.ToString(CultureInfo.InvariantCulture);
            if (fields.Select(prefix + "/image", fields.Localize("第 ") + (index + 1) + fields.Localize(" 帧图像"), ref texture, character.textures.Select((image, imageIndex) => (imageIndex.ToString(CultureInfo.InvariantCulture), image.name))))
            { frame.texture = int.Parse(texture, CultureInfo.InvariantCulture); changed = true; }
            double seconds = frame.seconds; changed |= fields.Number(prefix + "/duration", fields.Localize("停留秒数"), ref seconds, 0.001, 3600); frame.seconds = seconds;
            if (index > 0 && ImGui.SmallButton(fields.Caption("上移##" + prefix, fields.Localize("上移##") + prefix))) { moveFrom = index; moveTo = index - 1; }
            ImGui.SameLine(); if (index + 1 < animation.frames.Length && ImGui.SmallButton(fields.Caption("下移##" + prefix, fields.Localize("下移##") + prefix))) { moveFrom = index; moveTo = index + 1; }
            ImGui.SameLine(); if (ImGui.SmallButton(fields.Caption("删除此帧##" + prefix, fields.Localize("删除此帧##") + prefix))) remove = index;
        }
        if (moveFrom >= 0) { fields.Finish(); CharacterAnimationEditService.MoveFrame(animation, moveFrom, moveTo); changed = true; }
        if (remove >= 0) { fields.Finish(); CharacterAnimationEditService.RemoveFrame(animation, remove); changed = true; }
        ImGui.BeginDisabled(animation.frames.Length >= 256);
        try
        {
            if (ImGui.Button(fields.Caption("添加当前部位图像为一帧##" + key, fields.Localize("添加当前部位图像为一帧##") + key))) { fields.Finish(); CharacterAnimationEditService.AddFrame(character, part, part.texture); changed = true; }
            if (selectedImage is { } image && ImGui.Button(fields.Caption("添加所选图像为一帧##" + key, fields.Localize("添加所选图像为一帧##") + key))) { fields.Finish(); CharacterAnimationEditService.AddFrame(character, part, image); changed = true; }
        }
        finally { ImGui.EndDisabled(); }
        EditorUiSession.current.widgets.Hint(fields.Localize("序列结束后保留最后一帧；眨眼和未说话的嘴型使用当前表情图像作为静止帧。嘴型由舞台指令的 BeginSpeaking/EndSpeaking 标记控制，不是音素识别。删除最后一帧会关闭部位动画。"));
        return changed;
    }
}

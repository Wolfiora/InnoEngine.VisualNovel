using System;
using System.Linq;
using InnoEngine.Graphs;
using InnoEditor.ImGui;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Model.Performance;
using InnoEditor.Interactions;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Player.Interface.Performance;
using Inno.VisualNovel.Player.Effects;
using Inno.VisualNovel.Player.Sessions;
using System.Collections.Generic;

namespace Inno.VisualNovel.Editor.Inspection;

internal sealed class PerformanceNodeSettingsView(
    StoryInspectorFields fields,
    EditorInteractions interactions
)
{
    private NovelUiPerformance? m_ui;
    private IReadOnlyDictionary<string, PerformanceProperty[]> m_effects = new Dictionary<string, PerformanceProperty[]>();
    internal void Draw(
        StoryDocumentsModule documents,
        GraphNodeRecord node
    ) {
        PerformanceNode request = documents.codec.Read<PerformanceNode>(node);
        PerformanceOperation operation = request.operation;
        string id = request.instanceId, clip = request.clipId;
        bool changed = fields.SelectEnum(node.id + "/performance/op", "操作", ref operation);
        changed |= fields.Text(node.id + "/performance/id", fields.Localize("演出实例 ID"), ref id);
        request.operation = operation; request.instanceId = id;
        if (operation == PerformanceOperation.Play)
        {
            if (NovelResourceSelection.Get<NovelProjectAsset>(interactions) is { } game && ImGui.Button(fields.Label("使用所选游戏的 UI / 效果目标")))
            { m_ui = new(game); m_effects = NovelEffectPerformance.Targets(new StoryProgramLibrary(game).program, game.screenEffects); }
            changed |= fields.Text(node.id + "/performance/clip", fields.Localize("片段资源 ID"), ref clip); request.clipId = clip;
            if (NovelResourceSelection.Get<PerformanceClipAsset>(interactions) is { } selected && ImGui.Button(fields.Caption("使用所选片段：" + selected.displayName, fields.Localize("使用所选片段：") + selected.displayName)))
            {
                fields.Finish(); request.clipId = selected.resourceId;
                request.bindings = selected.tracks.Select(static track => track.target).Distinct(StringComparer.Ordinal).Select(slot =>
                    new PerformanceBinding { slot = slot, target = slot == "camera" ? "camera" : string.Empty }).ToArray();
                changed = true;
            }
            int remove = -1;
            for (int index = 0; index < request.bindings.Length; index++)
            {
                PerformanceBinding binding = request.bindings[index];
                string slot = binding.slot, target = binding.target;
                changed |= fields.Text(node.id + "/slot/" + index, fields.Localize("片段目标槽"), ref slot); binding.slot = slot;
                changed |= fields.Text(node.id + "/target/" + index, fields.Localize("舞台目标 ID"), ref target); binding.target = target;
                if (m_ui is not null) { changed |= fields.Select(node.id + "/ui-target/" + index, fields.Localize("选择 UI 目标"), ref target, m_ui.targets); binding.target = target; }
                if (m_effects.Count > 0) { changed |= fields.Select(node.id + "/effect-target/" + index, fields.Localize("选择效果实例"), ref target, m_effects.Keys.Select(static item => (item, item))); binding.target = target; }
                if (ImGui.Button(fields.Caption("移除此绑定##" + node.id + "/" + index, fields.Localize("移除此绑定##") + node.id + "/" + index))) { fields.Finish(); remove = index; }
            }
            if (remove >= 0) { request.bindings = request.bindings.Where((_, index) => index != remove).ToArray(); changed = true; }
            if (ImGui.Button(fields.Caption("添加目标绑定##" + node.id, fields.Localize("添加目标绑定##") + node.id)))
            { fields.Finish(); request.bindings = [.. request.bindings, new() { slot = "actor", target = string.Empty }]; changed = true; }
            EditorUiSession.current.widgets.Hint(fields.Localize("舞台目标使用 camera、character/实例名 或 stage/图层 ID；Volume 绑定 audio/槽位。UI 目标从所选游戏选择 ui/页面/元素，支持 X、Y、ScaleX（统一缩放）、Opacity。效果参数绑定 effect/实例，按预设机制限定可用通道。UI 元素须存在于该角色的所有主题中。位置为偏移，缩放/透明度/音量/效果强度为乘数；其余效果参数使用轨道数值。"));
        }
        if (operation == PerformanceOperation.Seek)
        {
            double time = request.time;
            changed |= fields.Number(node.id + "/time", fields.Localize("逻辑秒数"), ref time, 0); request.time = time;
        }
        if (operation == PerformanceOperation.WaitMarker)
        {
            string marker = request.markerId; changed |= fields.Text(node.id + "/marker", fields.Localize("首次到达的标记 ID"), ref marker); request.markerId = marker;
            if (NovelResourceSelection.Get<PerformanceClipAsset>(interactions) is { } selected)
            {
                changed |= fields.Select(node.id + "/marker-choice", fields.Localize("从所选片段选择标记"), ref marker, selected.markers.Select(static item => (item.id, item.name)));
                request.markerId = marker;
            }
            EditorUiSession.current.widgets.Hint(fields.Localize("等待该实例首次到达标记。已取消的实例立即结束等待；拖动/跳时只求值，不触发脚本或解锁。循环实例不会等待下一轮同名标记。"));
        }
        if (changed) documents.Edit(documents.activeDocumentId, fields.Localize("编辑演出片段请求"), state => documents.edits.SetSettings(state, node.id, request), fields.mergeKey);
    }
}

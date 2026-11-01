using InnoEngine.Graphs;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Model.Story.Nodes.Stage;

namespace Inno.VisualNovel.Editor.Inspection;

internal sealed class ImageEffectNodeSettingsView(
    StoryInspectorFields fields,
    EditorInteractions interactions
)
{
    internal void Screen(
        StoryDocumentsModule documents,
        GraphNodeRecord node
    ) {
        var request = documents.codec.Read<ScreenEffectNode>(node);
        var operation = request.operation; string instance = request.instanceId, resource = request.resourceId;
        bool changed = fields.SelectEnum(node.id + "/screen/op", "操作", ref operation); request.operation = operation;
        changed |= fields.Text(node.id + "/screen/instance", fields.Localize("效果实例 ID"), ref instance); request.instanceId = instance;
        if (operation is ScreenEffectOperation.Set or ScreenEffectOperation.Fade)
        {
            changed |= fields.Text(node.id + "/screen/resource", fields.Localize("屏幕效果资源 ID"), ref resource); request.resourceId = resource;
            if (NovelResourceSelection.Get<VisualEffectAsset>(interactions) is { kind: VisualEffectKind.Screen } selected && ImGui.Button(fields.Caption("使用所选效果：" + selected.displayName, fields.Localize("使用所选效果：") + selected.displayName)))
            { fields.Finish(); request.resourceId = selected.resourceId; changed = true; }
            double strength = request.strength, duration = request.duration, order = request.order; bool ui = request.includeUi, wait = request.waitForCompletion;
            changed |= fields.Number(node.id + "/screen/strength", fields.Localize("强度"), ref strength, 0, 1); request.strength = strength;
            changed |= fields.Number(node.id + "/screen/order", fields.Localize("叠加顺序"), ref order, -1000000, 1000000); request.order = (int)order;
            changed |= fields.Toggle(node.id + "/screen/ui", "同时处理游戏 UI", ref ui); request.includeUi = ui;
            if (operation == ScreenEffectOperation.Fade)
            {
                changed |= fields.Number(node.id + "/screen/duration", fields.Localize("渐变秒数"), ref duration, 0, 3600); request.duration = duration;
                changed |= fields.Toggle(node.id + "/screen/wait", "等待渐变完成", ref wait); request.waitForCompletion = wait;
            }
        }
        if (changed) documents.Edit(documents.activeDocumentId, fields.Localize("编辑屏幕效果"), state => documents.edits.SetSettings(state, node.id, request), fields.mergeKey);
        EditorUiSession.current.widgets.Hint(fields.Localize("同名实例明确替换旧效果；可使用等待、暂停、恢复、完成和移除控制生命周期。关闭闪烁时不会绘制闪白。"));
    }

    internal void Transition(
        StoryDocumentsModule documents,
        GraphNodeRecord node
    ) {
        var request = documents.codec.Read<TransitionNode>(node); var operation = request.operation;
        bool changed = fields.SelectEnum(node.id + "/transition/op", "操作", ref operation); request.operation = operation;
        if (operation == TransitionOperation.Begin)
        {
            string resource = request.resourceId, next = request.nextStage, target = request.target;
            var scope = request.scope; var group = request.group; bool wait = request.waitForCompletion;
            changed |= fields.Text(node.id + "/transition/resource", fields.Localize("转场资源 ID"), ref resource); request.resourceId = resource;
            if (NovelResourceSelection.Get<VisualEffectAsset>(interactions) is { kind: VisualEffectKind.Transition } selected && ImGui.Button(fields.Caption("使用所选转场：" + selected.displayName, fields.Localize("使用所选转场：") + selected.displayName)))
            { fields.Finish(); request.resourceId = selected.resourceId; changed = true; }
            changed |= fields.Text(node.id + "/transition/next", fields.Localize("切换到舞台（可空）"), ref next); request.nextStage = next;
            if (NovelResourceSelection.Get<StageAsset>(interactions) is { } stage && ImGui.Button(fields.Caption("使用所选目标舞台：" + stage.title, fields.Localize("使用所选目标舞台：") + stage.title)))
            { fields.Finish(); request.nextStage = stage.resourceId; changed = true; }
            changed |= fields.SelectEnum(node.id + "/transition/scope", "作用范围", ref scope); request.scope = scope;
            if (scope == TransitionScope.Group)
            {
                changed |= fields.SelectEnum(node.id + "/transition/group", "舞台分组", ref group); request.group = group;
                changed |= fields.Text(node.id + "/transition/target", fields.Localize("精确目标（可空）"), ref target); request.target = target;
            }
            changed |= fields.Toggle(node.id + "/transition/wait", "等待转场完成", ref wait); request.waitForCompletion = wait;
        }
        if (changed) documents.Edit(documents.activeDocumentId, fields.Localize("编辑转场"), state => documents.edits.SetSettings(state, node.id, request), fields.mergeKey);
        EditorUiSession.current.widgets.Hint(fields.Localize("开始时捕获旧画面，随后舞台操作形成新画面。Stage 保留实时 UI；Output 同时捕获旧 UI。取消只恢复旧舞台，不撤销剧情或全局副作用。"));
    }
}

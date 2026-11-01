using System;
using System.Linq;
using InnoEngine.Graphs;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using InnoEditor.Core;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Model.Performance;

namespace Inno.VisualNovel.Editor.Inspection;

internal sealed class StageNodeSettingsView(
    StoryInspectorFields fields,
    EditorInteractions interactions
)
{
    private CharacterAsset? m_character;
    private readonly CharacterPlacementView m_placements = new(fields, interactions);
    private StageAsset? m_referenceStage;
    private (string id, string label)[] m_followTargets = [];
    private Guid m_followDocument;
    private long m_followRevision = -1;
    private bool m_customFollow;
    internal void Draw(
        StoryDocumentsModule documents,
        StoryDraftState draft,
        GraphNodeRecord node
    ) {
        StageNode stage = documents.codec.Read<StageNode>(node);
        string id = node.id.value;
        StageOperation operation = stage.operation;
        bool changed = fields.SelectEnum(id + "/operation", "操作", ref operation);
        if (operation == StageOperation.FollowCamera && stage.operation != operation)
        {
            stage.transform = new() { pivotX = .5, pivotY = .5 };
            if (!StageCameraTargetRules.IsValid(stage.target)) stage.target = "character/" + stage.target;
        }
        stage.operation = operation;
        string resource = stage.resourceId, target = stage.target, pose = stage.pose, expression = stage.expression;
        if (NovelResourceSelection.Get<CharacterAsset>(interactions) is { isMissing: false } selectedCharacter) m_character = selectedCharacter;
        if (operation is StageOperation.SetStage or StageOperation.ShowCharacter or StageOperation.StartParticles) changed |= fields.Text(id + "/resource", fields.Localize("游戏资源 ID"), ref resource);
        string? selected = operation switch
        {
            StageOperation.SetStage => NovelResourceSelection.Get<StageAsset>(interactions)?.resourceId,
            StageOperation.ShowCharacter => NovelResourceSelection.Get<CharacterAsset>(interactions)?.resourceId,
            StageOperation.StartParticles => NovelResourceSelection.Get<VisualEffectAsset>(interactions)?.resourceId,
            _ => null
        };
        if (selected is not null && ImGui.Button(fields.Caption("使用所选资源：" + selected, fields.Localize("使用所选资源：") + selected))) { fields.Finish(); resource = selected; changed = true; }
        if (operation is StageOperation.ShowCharacter or StageOperation.HideCharacter or StageOperation.BeginSpeaking or StageOperation.EndSpeaking or StageOperation.StartParticles or StageOperation.StopParticles or StageOperation.RemoveParticles or StageOperation.WaitParticles)
            changed |= fields.Text(id + "/target", fields.Localize("实例名称"), ref target);
        if (operation == StageOperation.FollowCamera)
        {
            if (NovelResourceSelection.Get<StageAsset>(interactions) is { isMissing: false } referenceStage) m_referenceStage = referenceStage;
            long revision = documents.GetRevision(documents.activeDocumentId);
            if (m_followDocument != documents.activeDocumentId || m_followRevision != revision)
            {
                m_followTargets = draft.graph.document.nodes.Where(static item => item.definitionId == Model.Story.StoryNodeIds.stage)
                    .Select(documents.codec.Read<StageNode>).Where(static item => item.operation == StageOperation.ShowCharacter)
                    .Select(static item => ("character/" + item.target, "角色实例 · " + item.target)).Distinct().ToArray();
                m_followDocument = documents.activeDocumentId; m_followRevision = revision;
            }
            changed |= fields.Select(id + "/follow-target", fields.Localize("跟随目标"), ref target,
                new[] { (target, "当前 · " + target) }.Concat(m_followTargets)
                    .Concat(m_referenceStage?.layers.Select(static layer => ("stage/" + layer.id, "舞台图层 · " + layer.name)) ?? []).DistinctBy(static item => item.Item1));
            fields.Toggle(id + "/custom-follow", "手动指定其他章节目标", ref m_customFollow);
            if (m_customFollow) changed |= fields.Text(id + "/follow-id", fields.Localize("character/实例或 stage/图层"), ref target);
            double anchorX = stage.transform.pivotX, anchorY = stage.transform.pivotY;
            changed |= fields.Number(id + "/follow-anchor-x", fields.Localize("屏幕锚点 X（0～1）"), ref anchorX, 0, 1);
            changed |= fields.Number(id + "/follow-anchor-y", fields.Localize("屏幕锚点 Y（0～1）"), ref anchorY, 0, 1);
            stage.transform.pivotX = anchorX; stage.transform.pivotY = anchorY;
            EditorUiSession.current.widgets.Hint(fields.Localize("跟随作者枢轴和时间线位移，按当前缩放/旋转对齐屏幕锚点。目标须在执行前显示；选择舞台资源可列出它的图层。停止跟随或目标移除会冻结当前构图。"));
        }
        if (operation == StageOperation.ShowCharacter)
        {
            if (m_character is { isMissing: false } character && character.resourceId == resource)
            {
                changed |= fields.Select(id + "/pose", fields.Localize("姿势"), ref pose,
                    new[] { ("", fields.Localize("使用角色默认姿势")) }.Concat(character.poses.Select(static item => (item.id, item.name))));
                changed |= fields.Select(id + "/expression", fields.Localize("表情"), ref expression,
                    new[] { ("", fields.Localize("使用角色默认表情")) }.Concat(character.expressions.Select(static item => (item.id, item.name))));
            }
            else
            {
                changed |= fields.Text(id + "/pose", fields.Localize("姿势 ID，空值为默认"), ref pose);
                changed |= fields.Text(id + "/expression", fields.Localize("表情 ID，空值为默认"), ref expression);
                EditorUiSession.current.widgets.Hint(fields.Localize("在 File Browser 选中对应角色后，可按名称选择姿势和表情。"));
            }
            double order = stage.order;
            changed |= fields.Number(id + "/order", fields.Localize("角色组内顺序"), ref order, -1000000, 1000000); stage.order = (int)order;
            if (m_character is { isMissing: false } placementCharacter && placementCharacter.resourceId == resource)
                changed |= m_placements.Draw(id, placementCharacter, pose, expression, stage);
        }
        stage.resourceId = resource; stage.target = target; stage.pose = pose; stage.expression = expression;
        if (operation is StageOperation.ShowCharacter or StageOperation.SetCamera or StageOperation.FollowCamera or StageOperation.StartParticles)
        {
            double x = stage.transform.x, y = stage.transform.y, scaleX = stage.transform.scaleX, scaleY = stage.transform.scaleY;
            double rotation = stage.transform.rotation, opacity = stage.transform.opacity;
            changed |= fields.Number(id + "/x", "X", ref x, -1e6, 1e6);
            changed |= fields.Number(id + "/y", "Y", ref y, -1e6, 1e6);
            if (operation != StageOperation.StartParticles) changed |= fields.Number(id + "/scale-x", operation is StageOperation.SetCamera or StageOperation.FollowCamera ? fields.Localize("镜头缩放") : fields.Localize("横向缩放"), ref scaleX, operation is StageOperation.SetCamera or StageOperation.FollowCamera ? 0.001 : -1e6, 1e6);
            if (operation == StageOperation.ShowCharacter) changed |= fields.Number(id + "/scale-y", fields.Localize("纵向缩放"), ref scaleY, -1e6, 1e6);
            if (operation != StageOperation.StartParticles) changed |= fields.Number(id + "/rotation", fields.Localize("旋转角度"), ref rotation, -1e6, 1e6);
            if (operation == StageOperation.ShowCharacter) changed |= fields.Number(id + "/opacity", fields.Localize("透明度"), ref opacity, 0, 1);
            if (operation == StageOperation.ShowCharacter)
            {
                double pivotX = stage.transform.pivotX, pivotY = stage.transform.pivotY;
                changed |= fields.Number(id + "/pivot-x", fields.Localize("角色本地枢轴 X"), ref pivotX, -1e6, 1e6);
                changed |= fields.Number(id + "/pivot-y", fields.Localize("角色本地枢轴 Y"), ref pivotY, -1e6, 1e6);
                stage.transform.pivotX = pivotX; stage.transform.pivotY = pivotY;
            }
            stage.transform.x = x; stage.transform.y = y; stage.transform.scaleX = scaleX; stage.transform.scaleY = scaleY; stage.transform.rotation = rotation; stage.transform.opacity = opacity;
        }
        if (operation == StageOperation.StartParticles)
        {
            double seed = stage.seed;
            changed |= fields.Number(id + "/seed", fields.Localize("固定随机种子"), ref seed, 1, uint.MaxValue); stage.seed = (uint)seed;
        }
        if (changed) documents.Edit(documents.activeDocumentId, fields.Localize("编辑舞台演出"), state => documents.edits.SetSettings(state, node.id, stage), fields.mergeKey);
    }
}

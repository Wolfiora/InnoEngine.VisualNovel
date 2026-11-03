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
        bool changed = fields.SelectEnum(id + "/operation", "Operation", ref operation);
        if (operation == StageOperation.FollowCamera && stage.operation != operation)
        {
            stage.transform = new() { pivotX = .5, pivotY = .5 };
            if (!StageCameraTargetRules.IsValid(stage.target)) stage.target = "character/" + stage.target;
        }
        stage.operation = operation;
        string resource = stage.resourceId, target = stage.target, pose = stage.pose, expression = stage.expression;
        if (NovelResourceSelection.Get<CharacterAsset>(interactions) is { isMissing: false } selectedCharacter) m_character = selectedCharacter;
        if (operation is StageOperation.SetStage or StageOperation.ShowCharacter or StageOperation.StartParticles) changed |= fields.Text(id + "/resource", "Game resource ID", ref resource);
        string? selected = operation switch
        {
            StageOperation.SetStage => NovelResourceSelection.Get<StageAsset>(interactions)?.resourceId,
            StageOperation.ShowCharacter => NovelResourceSelection.Get<CharacterAsset>(interactions)?.resourceId,
            StageOperation.StartParticles => NovelResourceSelection.Get<VisualEffectAsset>(interactions)?.resourceId,
            _ => null
        };
        if (selected is not null && ImGui.Button("Use selected resource: " + selected)) { fields.Finish(); resource = selected; changed = true; }
        if (operation is StageOperation.ShowCharacter or StageOperation.HideCharacter or StageOperation.BeginSpeaking or StageOperation.EndSpeaking or StageOperation.StartParticles or StageOperation.StopParticles or StageOperation.RemoveParticles or StageOperation.WaitParticles)
            changed |= fields.Text(id + "/target", "Instance name", ref target);
        if (operation == StageOperation.FollowCamera)
        {
            if (NovelResourceSelection.Get<StageAsset>(interactions) is { isMissing: false } referenceStage) m_referenceStage = referenceStage;
            long revision = documents.GetRevision(documents.activeDocumentId);
            if (m_followDocument != documents.activeDocumentId || m_followRevision != revision)
            {
                m_followTargets = draft.graph.document.nodes.Where(static item => item.definitionId == Model.Story.StoryNodeIds.stage)
                    .Select(documents.codec.Read<StageNode>).Where(static item => item.operation == StageOperation.ShowCharacter)
                    .Select(static item => ("character/" + item.target, "Character instance · " + item.target)).Distinct().ToArray();
                m_followDocument = documents.activeDocumentId; m_followRevision = revision;
            }
            changed |= fields.Select(id + "/follow-target", "Follow target", ref target,
                new[] { (target, "Current · " + target) }.Concat(m_followTargets)
                    .Concat(m_referenceStage?.layers.Select(static layer => ("stage/" + layer.id, "Stage layer · " + layer.name)) ?? []).DistinctBy(static item => item.Item1));
            fields.Toggle(id + "/custom-follow", "Specify a target from another chapter manually", ref m_customFollow);
            if (m_customFollow) changed |= fields.Text(id + "/follow-id", "character/instance or stage/layer", ref target);
            double anchorX = stage.transform.pivotX, anchorY = stage.transform.pivotY;
            changed |= fields.Number(id + "/follow-anchor-x", "Screen anchor X (0–1)", ref anchorX, 0, 1);
            changed |= fields.Number(id + "/follow-anchor-y", "Screen anchor Y (0–1)", ref anchorY, 0, 1);
            stage.transform.pivotX = anchorX; stage.transform.pivotY = anchorY;
            EditorUiSession.current.widgets.Hint("Follow the authored pivot and timeline movement, aligning to the screen anchor with the current zoom and rotation. Show the target before executing this instruction. Select a stage asset to list its layers. Stopping follow or removing the target freezes the current composition.");
        }
        if (operation == StageOperation.ShowCharacter)
        {
            if (m_character is { isMissing: false } character && character.resourceId == resource)
            {
                changed |= fields.Select(id + "/pose", "Pose", ref pose,
                    new[] { ("", "Use character's default pose") }.Concat(character.poses.Select(static item => (item.id, item.name))));
                changed |= fields.Select(id + "/expression", "Expression", ref expression,
                    new[] { ("", "Use character's default expression") }.Concat(character.expressions.Select(static item => (item.id, item.name))));
            }
            else
            {
                changed |= fields.Text(id + "/pose", "Pose ID (empty for default)", ref pose);
                changed |= fields.Text(id + "/expression", "Expression ID (empty for default)", ref expression);
                EditorUiSession.current.widgets.Hint("Select the corresponding character in File Browser to choose poses and expressions by name.");
            }
            double order = stage.order;
            changed |= fields.Number(id + "/order", "Order within character group", ref order, -1000000, 1000000); stage.order = (int)order;
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
            if (operation != StageOperation.StartParticles) changed |= fields.Number(id + "/scale-x", operation is StageOperation.SetCamera or StageOperation.FollowCamera ? "Camera zoom" : "Scale X", ref scaleX, operation is StageOperation.SetCamera or StageOperation.FollowCamera ? 0.001 : -1e6, 1e6);
            if (operation == StageOperation.ShowCharacter) changed |= fields.Number(id + "/scale-y", "Scale Y", ref scaleY, -1e6, 1e6);
            if (operation != StageOperation.StartParticles) changed |= fields.Number(id + "/rotation", "Rotation angle", ref rotation, -1e6, 1e6);
            if (operation == StageOperation.ShowCharacter) changed |= fields.Number(id + "/opacity", "Opacity", ref opacity, 0, 1);
            if (operation == StageOperation.ShowCharacter)
            {
                double pivotX = stage.transform.pivotX, pivotY = stage.transform.pivotY;
                changed |= fields.Number(id + "/pivot-x", "Character local pivot X", ref pivotX, -1e6, 1e6);
                changed |= fields.Number(id + "/pivot-y", "Character local pivot Y", ref pivotY, -1e6, 1e6);
                stage.transform.pivotX = pivotX; stage.transform.pivotY = pivotY;
            }
            stage.transform.x = x; stage.transform.y = y; stage.transform.scaleX = scaleX; stage.transform.scaleY = scaleY; stage.transform.rotation = rotation; stage.transform.opacity = opacity;
        }
        if (operation == StageOperation.StartParticles)
        {
            double seed = stage.seed;
            changed |= fields.Number(id + "/seed", "Fixed random seed", ref seed, 1, uint.MaxValue); stage.seed = (uint)seed;
        }
        if (changed) documents.Edit(documents.activeDocumentId, "Edit stage presentation", state => documents.edits.SetSettings(state, node.id, stage), fields.mergeKey);
    }
}

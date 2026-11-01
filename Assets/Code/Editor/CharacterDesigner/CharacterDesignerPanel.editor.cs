using System;
using System.Linq;
using System.Numerics;
using InnoEngine.Rendering;
using InnoEditor.Core;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using InnoEditor.Rendering;
using Inno.VisualNovel.Authoring.CharacterEditing;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Stage.Composition;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Editor.CharacterDesigner;

/// <summary>Edits layered poses and nondestructive expressions over shared native resource documents.</summary>
[EditorPanel("inno.vn.character-designer", "VN 角色设计 · Character Designer", order: 453, menuPath: "Authoring")]
public sealed class CharacterDesignerPanel : EditorPanel, IEditorPanelReloadState
{
    private readonly ResourceDocumentsModule m_documents;
    private readonly EditorInteractions m_interactions;
    private readonly ResourceDocumentToolbar m_toolbar;
    private readonly StoryInspectorFields m_fields;
    private readonly NovelPreviewCanvas m_preview;
    private readonly CharacterAnimationFields m_animation;
    private readonly CharacterImageImportView m_import;
    private Guid m_previewDocument;
    private double m_previewTime;
    private bool m_previewPlaying, m_previewSpeaking;
    private string m_pose = string.Empty;
    private string m_expression = string.Empty;
    private string m_part = string.Empty;
    private string m_poseReplacement = string.Empty;
    private string m_expressionReplacement = string.Empty;
    private bool m_resizePart;
    private string m_message = string.Empty;
    /// <summary>Binds character resources, engine History and the native preview bridge.</summary>
    /// <param name="documents">The native resource draft owner.</param>
    /// <param name="interactions">The host selection and History coordinator.</param>
    /// <param name="previews">The offscreen native preview service.</param>
    public CharacterDesignerPanel(
        ResourceDocumentsModule documents,
        EditorInteractions interactions,
        IEditorPreviewService previews
    ) {
        m_documents = documents; m_interactions = interactions; m_fields = new(interactions);
        m_preview = new(previews); m_toolbar = new(documents, interactions, m_fields.Finish);
        m_animation = new(m_fields);
        m_import = new(documents, m_fields);
    }
    /// <inheritdoc />
    public override Vector2 initialSize => new(1020, 800);
    /// <inheritdoc />
    protected override void OnDraw(EditorContext context)
    {
        m_fields.BeginFrame();
        try { Draw(context.projectDirectory); }
        catch (Exception error) when (error is not OutOfMemoryException) { m_message = m_fields.Error(error); }
        finally { m_fields.EndFrame(); }
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
    }
    /// <inheritdoc />
    public ReadOnlyMemory<byte> CaptureReloadState() { m_fields.Finish(); m_import.Dispose(); return m_documents.CaptureReload(); }
    /// <inheritdoc />
    public void RestoreReloadState(ReadOnlyMemory<byte> state) => m_documents.RestoreReload(state.Span);
    /// <inheritdoc />
    protected override void OnDetach(EditorContext context) { m_fields.Finish(); m_import.Dispose(); m_preview.Dispose(); }
    private void Draw(string projectDirectory)
    {
        if (!m_toolbar.Draw("角色（使用所选图像）", ".ivncharacter", () => CharacterEditService.FromImage(NovelResourceSelection.Get<TextureAsset>(m_interactions)
            ?? throw new InvalidOperationException(m_fields.Localize("先在 File Browser 中选择角色图像。"))), out CharacterAsset? character) || character is null) return;
        if (m_previewDocument != m_toolbar.documentId)
        {
            m_previewDocument = m_toolbar.documentId; m_previewTime = 0; m_previewPlaying = m_previewSpeaking = false;
            m_poseReplacement = m_expressionReplacement = "";
        }
        bool changed = false;
        string resource = character.resourceId, name = character.displayName;
        changed |= m_fields.Text("character/resource", m_fields.Localize("剧情资源 ID"), ref resource); character.resourceId = resource;
        changed |= m_fields.Text("character/name", m_fields.Localize("角色名称"), ref name); character.displayName = name;
        string aliases = string.Join("\n", character.aliases); uint nameColor = character.nameColor;
        if (m_fields.MultilineText("character/aliases", m_fields.Localize("别名（每段一个，用于角色选择与识别）"), ref aliases))
        { character.aliases = aliases.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries); changed = true; }
        changed |= m_fields.Color("character/name-color", m_fields.Localize("对白姓名颜色"), ref nameColor); character.nameColor = nameColor;
        ImGui.Text(m_fields.Localize("对白头像 · ") + (character.portrait?.name ?? m_fields.Localize("未分配")));
        if (NovelResourceSelection.Get<TextureAsset>(m_interactions) is { } portrait && ImGui.Button(m_fields.Label("使用所选图像作为对白头像")))
        { m_fields.Finish(); character.portrait = portrait; changed = true; }
        if (character.portrait is not null && ImGui.Button(m_fields.Label("移除对白头像"))) { m_fields.Finish(); character.portrait = null; changed = true; }
        changed |= m_animation.Breathing(character.breathing);
        if (!character.poses.Any(item => item.id == m_pose)) m_pose = character.defaultPose;
        if (!character.expressions.Any(item => item.id == m_expression)) m_expression = character.defaultExpression;
        m_fields.Select("character/pose", m_fields.Localize("编辑姿势"), ref m_pose, character.poses.Select(static item => (item.id, item.name)));
        m_fields.Select("character/expression", m_fields.Localize("预览表情"), ref m_expression, character.expressions.Select(static item => (item.id, item.name)));
        CharacterPose pose = character.poses.Single(item => item.id == m_pose);
        EditorUiSession.current.widgets.Hint(m_fields.Localize("姿势 ID · ") + pose.id + (m_expression.Length > 0 ? m_fields.Localize("\n表情 ID · ") + m_expression : ""));
        string poseName = pose.name, defaultPose = character.defaultPose, defaultExpression = character.defaultExpression;
        changed |= m_fields.Text("character/pose-name/" + pose.id, m_fields.Localize("姿势名称"), ref poseName); pose.name = poseName;
        changed |= m_fields.Select("character/default-pose", m_fields.Localize("默认姿势"), ref defaultPose, character.poses.Select(static item => (item.id, item.name))); character.defaultPose = defaultPose;
        if (character.expressions.SingleOrDefault(item => item.id == m_expression) is { } namedExpression)
        {
            string expressionName = namedExpression.name;
            changed |= m_fields.Text("character/expression-name/" + namedExpression.id, m_fields.Localize("表情名称"), ref expressionName); namedExpression.name = expressionName;
            changed |= m_fields.Select("character/default-expression", m_fields.Localize("默认表情"), ref defaultExpression, character.expressions.Select(static item => (item.id, item.name)));
            character.defaultExpression = defaultExpression;
        }
        if (ImGui.Button(m_fields.Label("复制姿势")))
        { m_fields.Finish(); Edit(character, "复制角色姿势", candidate => m_pose = CharacterEditService.DuplicatePose(candidate, m_pose)); return; }
        ImGui.SameLine();
        if (ImGui.Button(m_fields.Label("添加表情")))
        {
            m_fields.Finish(); Edit(character, "添加角色表情", candidate => m_expression = CharacterVariantEditService.AddExpression(candidate)); return;
        }
        if (character.expressions.Any(item => item.id == m_expression) && ImGui.Button(m_fields.Label("复制当前表情")))
        { m_fields.Finish(); Edit(character, "复制角色表情", candidate => m_expression = CharacterVariantEditService.DuplicateExpression(candidate, m_expression)); return; }
        if (ImGui.CollapsingHeader(m_fields.Label("移除姿势或表情（保持稳定引用）")))
        {
            EditorUiSession.current.widgets.Hint(m_fields.Localize("移除后，剧情中明确引用此 ID 的指令需要修复；发布检查会定位引用。默认项需先明确选择替代项。"));
            if (character.poses.Length > 1)
            {
                if (character.defaultPose == m_pose) m_fields.Select("character/replace-pose", m_fields.Localize("替代默认姿势"), ref m_poseReplacement,
                    new[] { ("", m_fields.Localize("请选择替代项")) }.Concat(character.poses.Where(item => item.id != m_pose).Select(static item => (item.id, item.name))));
                if (ImGui.Button(m_fields.Label("移除当前姿势")))
                {
                    m_fields.Finish(); string next = character.defaultPose == m_pose ? m_poseReplacement : "";
                    Edit(character, "移除角色姿势", candidate => CharacterVariantEditService.RemovePose(candidate, m_pose, next)); m_pose = ""; return;
                }
            }
            if (character.expressions.Length > 1)
            {
                if (character.defaultExpression == m_expression) m_fields.Select("character/replace-expression", m_fields.Localize("替代默认表情"), ref m_expressionReplacement,
                    new[] { ("", m_fields.Localize("请选择替代项")) }.Concat(character.expressions.Where(item => item.id != m_expression).Select(static item => (item.id, item.name))));
                if (ImGui.Button(m_fields.Label("移除当前表情")))
                {
                    m_fields.Finish(); string next = character.defaultExpression == m_expression ? m_expressionReplacement : "";
                    Edit(character, "移除角色表情", candidate => CharacterVariantEditService.RemoveExpression(candidate, m_expression, next)); m_expression = ""; return;
                }
            }
        }
        if (NovelResourceSelection.Get<TextureAsset>(m_interactions) is { } image && ImGui.Button(m_fields.Label("将所选图像添加为所有姿势的部位")))
        { m_fields.Finish(); Edit(character, "添加角色部位", candidate => m_part = CharacterEditService.AddPart(candidate, image)); return; }
        foreach (CharacterPart part in pose.parts)
            if (ImGui.Selectable(m_fields.Caption((part.name.Length > 0 ? part.name : "部位 " + part.id) + "##" + part.id, (part.name.Length > 0 ? part.name : m_fields.Localize("部位 ") + part.id) + "##" + part.id), m_part == part.id)) { m_fields.Finish(); m_part = part.id; }
        CharacterPart? selected = pose.parts.SingleOrDefault(item => item.id == m_part);
        if (selected is not null)
        {
            string partName = selected.name;
            changed |= m_fields.Text("character/part-name/" + pose.id + "/" + selected.id, m_fields.Localize("此姿势中的部位名称"), ref partName); selected.name = partName;
            ImGui.Text(m_fields.Localize("部位基础图像 · ") + character.textures[selected.texture].name);
            ImGui.Checkbox(m_fields.Label("替换基础图像时同步像素尺寸"), ref m_resizePart);
            if (NovelResourceSelection.Get<TextureAsset>(m_interactions) is { } partImage && ImGui.Button(m_fields.Label("用所选图像替换此姿势的部位")))
            { m_fields.Finish(); Edit(character, "替换姿势部位图像", candidate => CharacterEditService.SetPartImage(candidate, m_pose, m_part, partImage, m_resizePart)); return; }
            changed |= StageTransformFields.Draw(m_fields, selected.id, selected.transform);
            int mask = selected.mask;
            changed |= StageSurfaceFields.Draw(m_fields, selected.id, selected.surface, character.textures, ref mask); selected.mask = mask;
            if (NovelResourceSelection.Get<TextureAsset>(m_interactions) is { } maskImage && ImGui.Button(m_fields.Label("使用所选图像作为部位遮罩")))
            { m_fields.Finish(); selected.mask = CharacterEditService.ImageIndex(character, maskImage); changed = true; }
            double width = selected.width, height = selected.height, order = selected.order;
            changed |= m_fields.Number(selected.id + "/width", m_fields.Localize("部位宽度"), ref width, 0, 100000); selected.width = width;
            changed |= m_fields.Number(selected.id + "/height", m_fields.Localize("部位高度"), ref height, 0, 100000); selected.height = height;
            changed |= m_fields.Number(selected.id + "/order", m_fields.Localize("部位顺序"), ref order, -100000, 100000); selected.order = (int)order;
            bool visible = selected.visible;
            changed |= m_fields.Toggle(selected.id + "/visible", "姿势中可见", ref visible); selected.visible = visible;
            uint color = selected.color; changed |= m_fields.Color(selected.id + "/tint", m_fields.Localize("部位颜色（不修改源图像）"), ref color); selected.color = color;
            int? animationImage = null;
            if (NovelResourceSelection.Get<TextureAsset>(m_interactions) is { } frameImage)
            {
                animationImage = Array.FindIndex(character.textures, candidateImage => candidateImage.identity.persistentId == frameImage.identity.persistentId);
                if (animationImage < 0 && ImGui.Button(m_fields.Label("将所选图像加入动画图像库"))) { m_fields.Finish(); animationImage = CharacterEditService.ImageIndex(character, frameImage); changed = true; }
                if (animationImage < 0) animationImage = null;
            }
            changed |= m_animation.Part(character, selected, animationImage);
            CharacterExpression? expression = character.expressions.SingleOrDefault(item => item.id == m_expression);
            if (expression is not null)
            {
                CharacterPartOverride replacement = expression.parts.SingleOrDefault(item => item.partId == selected.id) ?? new() { partId = selected.id };
                bool expressionChanged = false;
                double visibility = replacement.visibility, offsetX = replacement.offsetX, offsetY = replacement.offsetY;
                expressionChanged |= m_fields.Number(selected.id + "/expression-visibility", m_fields.Localize("表情显隐（-1 保持）"), ref visibility, -1, 1);
                expressionChanged |= m_fields.Number(selected.id + "/expression-x", m_fields.Localize("表情水平偏移"), ref offsetX);
                expressionChanged |= m_fields.Number(selected.id + "/expression-y", m_fields.Localize("表情垂直偏移"), ref offsetY);
                replacement.visibility = (int)visibility; replacement.offsetX = offsetX; replacement.offsetY = offsetY;
                if (NovelResourceSelection.Get<TextureAsset>(m_interactions) is { } replacementImage && ImGui.Button(m_fields.Label("将所选图像用作此表情差分")))
                { m_fields.Finish(); replacement.texture = CharacterEditService.ImageIndex(character, replacementImage); expressionChanged = true; }
                if (expressionChanged) { expression.parts = [.. expression.parts.Where(item => item.partId != replacement.partId), replacement]; changed = true; }
                if (expression.parts.Any(item => item.partId == selected.id) && ImGui.Button(m_fields.Label("清除此部位表情覆盖，恢复姿势原值")))
                { m_fields.Finish(); Edit(character, "清除角色部位表情覆盖", candidate => CharacterVariantEditService.ClearPartOverride(candidate, m_expression, m_part)); return; }
            }
            if (ImGui.Button(m_fields.Label("从全部姿势与表情移除此部位")))
            { m_fields.Finish(); Edit(character, "删除共享角色部位与差分", candidate => CharacterEditService.RemovePart(candidate, m_part)); m_part = string.Empty; return; }
        }
        if (changed) Edit(character, "编辑角色", mergeKey: m_fields.mergeKey);
        if (m_import.Draw(projectDirectory, m_toolbar.documentId, character)) return;
        if (ImGui.Button(m_fields.Caption(m_previewPlaying ? "暂停角色预览" : "播放角色预览", m_previewPlaying ? m_fields.Localize("暂停角色预览") : m_fields.Localize("播放角色预览")))) m_previewPlaying = !m_previewPlaying;
        ImGui.SameLine(); if (ImGui.Button(m_fields.Label("重置预览时间"))) m_previewTime = 0;
        m_fields.Number("character/preview-time", m_fields.Localize("预览逻辑秒数"), ref m_previewTime, 0, 86400);
        ImGui.Checkbox(m_fields.Label("预览说话标记"), ref m_previewSpeaking);
        if (m_previewPlaying) m_previewTime = Math.Min(86400, m_previewTime + Math.Clamp(ImGui.GetInput().deltaTime, 0, 0.25));
        CharacterAsset shown = m_import.displayCharacter ?? character;
        string shownPose = m_import.displayCharacter is null ? pose.id : m_import.displayPose;
        string shownExpression = m_import.displayCharacter is null ? character.expressions.Any(item => item.id == m_expression) ? m_expression : character.defaultExpression : m_import.displayExpression;
        var stage = new StageController(shown.textures);
        stage.ShowCharacter("preview", shown, shownPose, shownExpression, new() { x = 660, y = 40 });
        if (m_previewSpeaking) stage.SetCharacterSpeaking("preview", true);
        stage.Tick(m_previewTime);
        float available = Math.Max(100, ImGui.GetContentRegionAvailable().X);
        m_preview.Draw(new UiFrame(1920, 1080, []), new(available, available * 1080 / 1920), stage.BuildFrame(1920, 1080));
    }
    private void Edit(
        CharacterAsset source,
        string label,
        Action<CharacterAsset>? edit = null,
        string? mergeKey = null
    ) => m_documents.Edit<CharacterAsset>(m_toolbar.documentId, label, candidate =>
    {
        CharacterAssetCopy.CopyInto(source, candidate);
        edit?.Invoke(candidate);
    }, mergeKey);
}

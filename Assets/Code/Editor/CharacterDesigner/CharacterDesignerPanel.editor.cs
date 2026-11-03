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
[EditorPanel("inno.vn.character-designer", "VN Character Designer", order: 453, defaultOpen: false, menuPath: "Authoring")]
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
        catch (Exception error) when (error is not OutOfMemoryException) { m_message = (error).Message; }
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
        if (!m_toolbar.Draw("Character (from selected image)", ".ivncharacter", () => CharacterEditService.FromImage(NovelResourceSelection.Get<TextureAsset>(m_interactions)
            ?? throw new InvalidOperationException("Select a character image in File Browser first.")), out CharacterAsset? character) || character is null) return;
        if (m_previewDocument != m_toolbar.documentId)
        {
            m_previewDocument = m_toolbar.documentId; m_previewTime = 0; m_previewPlaying = m_previewSpeaking = false;
            m_poseReplacement = m_expressionReplacement = "";
        }
        bool changed = false;
        string resource = character.resourceId, name = character.displayName;
        changed |= m_fields.Text("character/resource", "Story resource ID", ref resource); character.resourceId = resource;
        changed |= m_fields.Text("character/name", "Character name", ref name); character.displayName = name;
        string aliases = string.Join("\n", character.aliases); uint nameColor = character.nameColor;
        if (m_fields.MultilineText("character/aliases", "Aliases (one per paragraph, used for selection and matching)", ref aliases))
        { character.aliases = aliases.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries); changed = true; }
        changed |= m_fields.Color("character/name-color", "Dialogue name color", ref nameColor); character.nameColor = nameColor;
        ImGui.Text("Dialogue portrait · " + (character.portrait?.name ?? "Unassigned"));
        if (NovelResourceSelection.Get<TextureAsset>(m_interactions) is { } portrait && ImGui.Button("Use selected image as dialogue portrait"))
        { m_fields.Finish(); character.portrait = portrait; changed = true; }
        if (character.portrait is not null && ImGui.Button("Remove dialogue portrait")) { m_fields.Finish(); character.portrait = null; changed = true; }
        changed |= m_animation.Breathing(character.breathing);
        if (!character.poses.Any(item => item.id == m_pose)) m_pose = character.defaultPose;
        if (!character.expressions.Any(item => item.id == m_expression)) m_expression = character.defaultExpression;
        m_fields.Select("character/pose", "Edit pose", ref m_pose, character.poses.Select(static item => (item.id, item.name)));
        m_fields.Select("character/expression", "Preview expression", ref m_expression, character.expressions.Select(static item => (item.id, item.name)));
        CharacterPose pose = character.poses.Single(item => item.id == m_pose);
        EditorUiSession.current.widgets.Hint("Pose ID · " + pose.id + (m_expression.Length > 0 ? "\nExpression ID · " + m_expression : ""));
        string poseName = pose.name, defaultPose = character.defaultPose, defaultExpression = character.defaultExpression;
        changed |= m_fields.Text("character/pose-name/" + pose.id, "Pose name", ref poseName); pose.name = poseName;
        changed |= m_fields.Select("character/default-pose", "Default pose", ref defaultPose, character.poses.Select(static item => (item.id, item.name))); character.defaultPose = defaultPose;
        if (character.expressions.SingleOrDefault(item => item.id == m_expression) is { } namedExpression)
        {
            string expressionName = namedExpression.name;
            changed |= m_fields.Text("character/expression-name/" + namedExpression.id, "Expression name", ref expressionName); namedExpression.name = expressionName;
            changed |= m_fields.Select("character/default-expression", "Default expression", ref defaultExpression, character.expressions.Select(static item => (item.id, item.name)));
            character.defaultExpression = defaultExpression;
        }
        if (ImGui.Button("Duplicate pose"))
        { m_fields.Finish(); Edit(character, "Duplicate character pose", candidate => m_pose = CharacterEditService.DuplicatePose(candidate, m_pose)); return; }
        ImGui.SameLine();
        if (ImGui.Button("Add expression"))
        {
            m_fields.Finish(); Edit(character, "Add character expression", candidate => m_expression = CharacterVariantEditService.AddExpression(candidate)); return;
        }
        if (character.expressions.Any(item => item.id == m_expression) && ImGui.Button("Duplicate current expression"))
        { m_fields.Finish(); Edit(character, "Duplicate character expression", candidate => m_expression = CharacterVariantEditService.DuplicateExpression(candidate, m_expression)); return; }
        if (ImGui.CollapsingHeader("Remove pose or expression (preserve stable references)"))
        {
            EditorUiSession.current.widgets.Hint("Removing this item leaves explicit story references to its ID unresolved. Publishing checks locate those references. Choose a replacement before removing a default item.");
            if (character.poses.Length > 1)
            {
                if (character.defaultPose == m_pose) m_fields.Select("character/replace-pose", "Replacement default pose", ref m_poseReplacement,
                    new[] { ("", "Choose a replacement") }.Concat(character.poses.Where(item => item.id != m_pose).Select(static item => (item.id, item.name))));
                if (ImGui.Button("Remove current pose"))
                {
                    m_fields.Finish(); string next = character.defaultPose == m_pose ? m_poseReplacement : "";
                    Edit(character, "Remove character pose", candidate => CharacterVariantEditService.RemovePose(candidate, m_pose, next)); m_pose = ""; return;
                }
            }
            if (character.expressions.Length > 1)
            {
                if (character.defaultExpression == m_expression) m_fields.Select("character/replace-expression", "Replacement default expression", ref m_expressionReplacement,
                    new[] { ("", "Choose a replacement") }.Concat(character.expressions.Where(item => item.id != m_expression).Select(static item => (item.id, item.name))));
                if (ImGui.Button("Remove current expression"))
                {
                    m_fields.Finish(); string next = character.defaultExpression == m_expression ? m_expressionReplacement : "";
                    Edit(character, "Remove character expression", candidate => CharacterVariantEditService.RemoveExpression(candidate, m_expression, next)); m_expression = ""; return;
                }
            }
        }
        if (NovelResourceSelection.Get<TextureAsset>(m_interactions) is { } image && ImGui.Button("Add selected image as a part in every pose"))
        { m_fields.Finish(); Edit(character, "Add character part", candidate => m_part = CharacterEditService.AddPart(candidate, image)); return; }
        foreach (CharacterPart part in pose.parts)
            if (ImGui.Selectable((part.name.Length > 0 ? part.name : "Part " + part.id) + "##" + part.id, m_part == part.id)) { m_fields.Finish(); m_part = part.id; }
        CharacterPart? selected = pose.parts.SingleOrDefault(item => item.id == m_part);
        if (selected is not null)
        {
            string partName = selected.name;
            changed |= m_fields.Text("character/part-name/" + pose.id + "/" + selected.id, "Part name in this pose", ref partName); selected.name = partName;
            ImGui.Text("Part base image · " + character.textures[selected.texture].name);
            ImGui.Checkbox("Match pixel dimensions when replacing the base image", ref m_resizePart);
            if (NovelResourceSelection.Get<TextureAsset>(m_interactions) is { } partImage && ImGui.Button("Replace this pose's part with the selected image"))
            { m_fields.Finish(); Edit(character, "Replace pose part image", candidate => CharacterEditService.SetPartImage(candidate, m_pose, m_part, partImage, m_resizePart)); return; }
            changed |= StageTransformFields.Draw(m_fields, selected.id, selected.transform);
            int mask = selected.mask;
            changed |= StageSurfaceFields.Draw(m_fields, selected.id, selected.surface, character.textures, ref mask); selected.mask = mask;
            if (NovelResourceSelection.Get<TextureAsset>(m_interactions) is { } maskImage && ImGui.Button("Use selected image as part mask"))
            { m_fields.Finish(); selected.mask = CharacterEditService.ImageIndex(character, maskImage); changed = true; }
            double width = selected.width, height = selected.height, order = selected.order;
            changed |= m_fields.Number(selected.id + "/width", "Part width", ref width, 0, 100000); selected.width = width;
            changed |= m_fields.Number(selected.id + "/height", "Part height", ref height, 0, 100000); selected.height = height;
            changed |= m_fields.Number(selected.id + "/order", "Part order", ref order, -100000, 100000); selected.order = (int)order;
            bool visible = selected.visible;
            changed |= m_fields.Toggle(selected.id + "/visible", "Visible in this pose", ref visible); selected.visible = visible;
            uint color = selected.color; changed |= m_fields.Color(selected.id + "/tint", "Part tint (preserves source pixels)", ref color); selected.color = color;
            int? animationImage = null;
            if (NovelResourceSelection.Get<TextureAsset>(m_interactions) is { } frameImage)
            {
                animationImage = Array.FindIndex(character.textures, candidateImage => candidateImage.identity.persistentId == frameImage.identity.persistentId);
                if (animationImage < 0 && ImGui.Button("Add selected image to the animation library")) { m_fields.Finish(); animationImage = CharacterEditService.ImageIndex(character, frameImage); changed = true; }
                if (animationImage < 0) animationImage = null;
            }
            changed |= m_animation.Part(character, selected, animationImage);
            CharacterExpression? expression = character.expressions.SingleOrDefault(item => item.id == m_expression);
            if (expression is not null)
            {
                CharacterPartOverride replacement = expression.parts.SingleOrDefault(item => item.partId == selected.id) ?? new() { partId = selected.id };
                bool expressionChanged = false;
                double visibility = replacement.visibility, offsetX = replacement.offsetX, offsetY = replacement.offsetY;
                expressionChanged |= m_fields.Number(selected.id + "/expression-visibility", "Expression visibility (-1 keeps the pose value)", ref visibility, -1, 1);
                expressionChanged |= m_fields.Number(selected.id + "/expression-x", "Expression horizontal offset", ref offsetX);
                expressionChanged |= m_fields.Number(selected.id + "/expression-y", "Expression vertical offset", ref offsetY);
                replacement.visibility = (int)visibility; replacement.offsetX = offsetX; replacement.offsetY = offsetY;
                if (NovelResourceSelection.Get<TextureAsset>(m_interactions) is { } replacementImage && ImGui.Button("Use selected image for this expression variant"))
                { m_fields.Finish(); replacement.texture = CharacterEditService.ImageIndex(character, replacementImage); expressionChanged = true; }
                if (expressionChanged) { expression.parts = [.. expression.parts.Where(item => item.partId != replacement.partId), replacement]; changed = true; }
                if (expression.parts.Any(item => item.partId == selected.id) && ImGui.Button("Clear this part's expression override and restore its pose values"))
                { m_fields.Finish(); Edit(character, "Clear character part expression override", candidate => CharacterVariantEditService.ClearPartOverride(candidate, m_expression, m_part)); return; }
            }
            if (ImGui.Button("Remove this part from every pose and expression"))
            { m_fields.Finish(); Edit(character, "Remove shared character part and variants", candidate => CharacterEditService.RemovePart(candidate, m_part)); m_part = string.Empty; return; }
        }
        if (changed) Edit(character, "Edit character", mergeKey: m_fields.mergeKey);
        if (m_import.Draw(projectDirectory, m_toolbar.documentId, character)) return;
        if (ImGui.Button(m_previewPlaying ? "Pause character preview" : "Play character preview")) m_previewPlaying = !m_previewPlaying;
        ImGui.SameLine(); if (ImGui.Button("Reset preview time")) m_previewTime = 0;
        m_fields.Number("character/preview-time", "Preview time (logical seconds)", ref m_previewTime, 0, 86400);
        ImGui.Checkbox("Preview speaking marker", ref m_previewSpeaking);
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

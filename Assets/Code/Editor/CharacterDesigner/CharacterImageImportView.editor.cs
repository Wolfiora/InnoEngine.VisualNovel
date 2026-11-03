using System;
using System.Linq;
using InnoEditor.ImGui;
using InnoEditor.Core;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.CharacterEditing.ImageImport;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Model.Characters;

namespace Inno.VisualNovel.Editor.CharacterDesigner;

internal sealed class CharacterImageImportView(
    ResourceDocumentsModule documents,
    StoryInspectorFields fields
) : IDisposable
{
    private NativeCharacterImagePreparation? m_job;
    private CharacterAsset? m_source, m_display;
    private CharacterImageBatchPreview? m_review;
    private Guid m_document;
    private long m_revision;
    private string m_folder = string.Empty, m_reviewFolder = string.Empty, m_message = string.Empty;
    private bool m_createVariants, m_resize, m_showCandidate;
    private string m_pose = string.Empty, m_expression = string.Empty;
    private int m_page;
    internal CharacterAsset? displayCharacter => m_showCandidate ? m_display : null;
    internal string displayPose => m_pose;
    internal string displayExpression => m_expression;

    internal bool Draw(
        string projectDirectory,
        Guid document,
        CharacterAsset source
    ) {
        long revision = documents.GetRevision(document);
        if (m_document != document || m_revision != revision)
        { Release(); m_document = document; m_revision = revision; }
        Update();
        if (!ImGui.CollapsingHeader("Match and preview variant images in bulk")) return false;
        EditorUiSession.current.widgets.Hint("Use pose__pose-name__part-name.png or expression__expression-name__part-name.png. Stable IDs are also accepted. Imported PNG/JPEG images are supported. Only direct files in the selected folder are read.");
        bool optionsChanged = fields.Text("character/batch/folder", "Image folder (relative to Assets)", ref m_folder);
        optionsChanged |= fields.Toggle("character/batch/create", "Allow named poses and expressions to be created", ref m_createVariants);
        optionsChanged |= fields.Toggle("character/batch/resize", "Match pixel dimensions when replacing pose images", ref m_resize);
        if (optionsChanged) Release();
        if (ImGui.Button("Prepare and check image matches"))
        {
            fields.Finish();
            var candidate = new NativeCharacterImagePreparation(projectDirectory, m_folder);
            Release(); m_job = candidate; m_source = CharacterAssetCopy.Copy(source); m_reviewFolder = m_folder; m_message = "";
        }
        if (m_job is { } job)
        {
            ImGui.Text("Preparing imported images · " + job.processed + " / " + job.total);
            if (ImGui.Button("Cancel image preparation")) { Release(); m_message = "Cancelled. The character document is unchanged."; }
        }
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
        if (m_review is not { } review) return false;
        ImGui.Text("Reviewed source · " + m_reviewFolder + " · Ready: " + review.readyCount + " / " + review.rows.Count);
        EditorUiSession.current.widgets.Hint("Duplicate and unmatched items are excluded. New poses copy the default pose. Expressions override only matched images, preserving transforms, masks and other overrides. Prepare again if an image or the document changes.");
        if (review.readyCount > 0)
        {
            ImGui.Checkbox("Preview the reviewed candidate on the stage below (without applying)", ref m_showCandidate);
            if (m_showCandidate && m_display is { } display)
            {
                fields.Select("character/batch/preview-pose", "Candidate preview pose", ref m_pose, display.poses.Select(static item => (item.id, item.name)));
                if (display.expressions.Length > 0) fields.Select("character/batch/preview-expression", "Candidate preview expression", ref m_expression, display.expressions.Select(static item => (item.id, item.name)));
            }
            if (ImGui.Button("Apply all " + review.readyCount + " ready matches (one undoable operation)"))
            {
                fields.Finish();
                try
                {
                    documents.Edit<CharacterAsset>(document, "Apply character variant image batch", candidate => CharacterImageBatchService.ApplyReady(candidate, review));
                    Release(); m_message = "Applied the ready matches from this review. Other entries retain their original values. Undo restores the whole batch."; return true;
                }
                catch (Exception error) when (error is not OutOfMemoryException) { m_message = (error).Message; }
            }
        }
        int pages = Math.Max(1, (review.rows.Count + 19) / 20); m_page = Math.Clamp(m_page, 0, pages - 1);
        if (m_page > 0 && ImGui.Button("Previous page##character-batch")) m_page--;
        ImGui.SameLine(); ImGui.Text((m_page + 1) + " / " + pages);
        ImGui.SameLine(); if (m_page + 1 < pages && ImGui.Button("Next page##character-batch")) m_page++;
        foreach (var row in review.rows.Skip(m_page * 20).Take(20))
        {
            ImGui.Text((row.applicable ? "Ready · " : "Needs attention · ") + row.variant + " / " + row.part);
            EditorUiSession.current.widgets.Hint(row.path + "\n" + row.message);
        }
        return false;
    }

    private void Update()
    {
        if (m_job is not { } job) return;
        try
        {
            job.Step(); if (!job.finished) return;
            if (job.error.Length > 0) { m_message = job.error; return; }
            var review = CharacterImageBatchService.Preview(m_source!, job.inputs, m_createVariants, m_resize);
            m_review = review; m_display = review.CopyForDisplay(); m_page = 0;
            m_pose = review.rows.FirstOrDefault(static row => row.applicable && row.kind == CharacterImageBatchKind.Pose)?.variantId ?? m_display.defaultPose;
            m_expression = review.rows.FirstOrDefault(static row => row.applicable && row.kind == CharacterImageBatchKind.Expression)?.variantId ?? m_display.defaultExpression;
            m_message = review.rows.Count == 0 ? "The folder contains no PNG/JPEG images to prepare." : "";
        }
        catch (Exception error) when (error is not OutOfMemoryException) { m_message = (error).Message; }
        finally
        {
            if (job.finished) { job.Dispose(); m_job = null; m_source = null; }
        }
    }
    private void Release()
    { m_job?.Dispose(); m_job = null; m_source = null; m_display = null; m_review = null; m_showCandidate = false; m_page = 0; }
    public void Dispose() => Release();
}

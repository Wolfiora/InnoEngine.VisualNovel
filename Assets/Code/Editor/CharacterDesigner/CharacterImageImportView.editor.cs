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
        if (!ImGui.CollapsingHeader(fields.Label("批量差分图像匹配与预览"))) return false;
        EditorUiSession.current.widgets.Hint(fields.Localize("文件名：pose__姿势名称__部位名称.png 或 expression__表情名称__部位名称.png。也可使用稳定 ID；支持已导入的 PNG/JPEG。只读取所选文件夹的直接文件。"));
        bool optionsChanged = fields.Text("character/batch/folder", fields.Localize("图片文件夹（相对 Assets）"), ref m_folder);
        optionsChanged |= fields.Toggle("character/batch/create", "允许创建命名姿势/表情", ref m_createVariants);
        optionsChanged |= fields.Toggle("character/batch/resize", "替换姿势图像时同步像素尺寸", ref m_resize);
        if (optionsChanged) Release();
        if (ImGui.Button(fields.Label("准备并检查图像匹配")))
        {
            fields.Finish();
            var candidate = new NativeCharacterImagePreparation(projectDirectory, m_folder);
            Release(); m_job = candidate; m_source = CharacterAssetCopy.Copy(source); m_reviewFolder = m_folder; m_message = "";
        }
        if (m_job is { } job)
        {
            ImGui.Text(fields.Localize("准备原生图像 · ") + job.processed + " / " + job.total);
            if (ImGui.Button(fields.Label("取消图像准备"))) { Release(); m_message = fields.Localize("已取消，角色文档未更改。"); }
        }
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
        if (m_review is not { } review) return false;
        ImGui.Text(fields.Localize("审核来源 · ") + m_reviewFolder + fields.Localize(" · 可用 ") + review.readyCount + " / " + review.rows.Count);
        EditorUiSession.current.widgets.Hint(fields.Localize("重复或无法匹配的条目不会应用。新姿势复制默认姿势，表情只覆盖匹配图像；变换、遮罩与其他覆盖保留。图像或文档变化后需重新准备。"));
        if (review.readyCount > 0)
        {
            ImGui.Checkbox(fields.Label("在下方舞台显示冻结的批次候选（未提交）"), ref m_showCandidate);
            if (m_showCandidate && m_display is { } display)
            {
                fields.Select("character/batch/preview-pose", fields.Localize("候选预览姿势"), ref m_pose, display.poses.Select(static item => (item.id, item.name)));
                if (display.expressions.Length > 0) fields.Select("character/batch/preview-expression", fields.Localize("候选预览表情"), ref m_expression, display.expressions.Select(static item => (item.id, item.name)));
            }
            if (ImGui.Button(fields.Caption("应用全部 " + review.readyCount + " 项可用匹配（一个可撤销操作）", fields.Localize("应用全部 ") + review.readyCount + fields.Localize(" 项可用匹配（一个可撤销操作）"))))
            {
                fields.Finish();
                try
                {
                    documents.Edit<CharacterAsset>(document, fields.Localize("应用角色差分图像批次"), candidate => CharacterImageBatchService.ApplyReady(candidate, review));
                    Release(); m_message = fields.Localize("已应用审核中的可用匹配；其他条目保留原值。可用引擎 Undo 撤销整个批次。"); return true;
                }
                catch (Exception error) when (error is not OutOfMemoryException) { m_message = fields.Error(error); }
            }
        }
        int pages = Math.Max(1, (review.rows.Count + 19) / 20); m_page = Math.Clamp(m_page, 0, pages - 1);
        if (m_page > 0 && ImGui.Button(fields.Label("上一页##character-batch"))) m_page--;
        ImGui.SameLine(); ImGui.Text((m_page + 1) + " / " + pages);
        ImGui.SameLine(); if (m_page + 1 < pages && ImGui.Button(fields.Label("下一页##character-batch"))) m_page++;
        foreach (var row in review.rows.Skip(m_page * 20).Take(20))
        {
            ImGui.Text((row.applicable ? fields.Localize("可用 · ") : fields.Localize("待处理 · ")) + row.variant + " / " + row.part);
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
            m_message = review.rows.Count == 0 ? fields.Localize("文件夹没有可准备的 PNG/JPEG 图像。") : "";
        }
        catch (Exception error) when (error is not OutOfMemoryException) { m_message = fields.Error(error); }
        finally
        {
            if (job.finished) { job.Dispose(); m_job = null; m_source = null; }
        }
    }
    private void Release()
    { m_job?.Dispose(); m_job = null; m_source = null; m_display = null; m_review = null; m_showCandidate = false; m_page = 0; }
    public void Dispose() => Release();
}

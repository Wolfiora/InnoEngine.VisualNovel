using System;
using System.IO;
using System.Numerics;
using InnoEngine.Assets;
using InnoEditor.Core;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.Publishing;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Editor.Localization;

namespace Inno.VisualNovel.Editor.Publishing;

/// <summary>Presents native content diagnostics and opens the engine's existing export action.</summary>
[EditorPanel("inno.vn.publishing", "VN 发布检查 · Publishing Checks", order: 466, menuPath: "Authoring")]
public sealed class PublishingPanel : EditorPanel
{
    private readonly EditorTextView m_text;
    private readonly EditorInteractions m_interactions;
    private NovelPublishReport? m_report;
    private string m_message = string.Empty;
    /// <summary>Binds the engine selection, resource locator and export interaction.</summary>
    /// <param name="interactions">The current editor generation's public action coordinator.</param>
    public PublishingPanel(EditorInteractions interactions) { m_interactions = interactions; m_text = new(interactions); }
    /// <inheritdoc />
    public override Vector2 initialSize => new(920, 720);
    /// <inheritdoc />
    protected override void OnDraw(EditorContext context)
    {
        try { Draw(context); }
        catch (Exception error) when (error is not OutOfMemoryException) { m_message = m_text.Error(error); }
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
    }
    private void Draw(EditorContext context)
    {
        EditorUiSession.current.widgets.Hint(m_text.Localize("先保存全部文档，再在 File Browser 选择 .ivnproject。检查已导入内容，最终游戏由引擎 Export as Game 构建。"));
        if (ImGui.Button(m_text.Label("检查所选游戏 / Check selected game")))
        {
            NovelProjectAsset project = NovelResourceSelection.Get<NovelProjectAsset>(m_interactions) ?? throw new InvalidOperationException(m_text.Localize("请选择已导入的 .ivnproject。"));
            m_report = NovelPublishPreflight.Check(project); m_message = string.Empty;
        }
        var export = m_interactions.For("inno.vn.publishing", m_report);
        EditorActionState availability = export.Query("export/game");
        ImGui.SameLine();
        ImGui.BeginDisabled(m_report?.contentValid != true || !availability.isVisible || !availability.isEnabled);
        try { if (ImGui.Button(m_text.Label("打开引擎游戏导出"))) export.Enqueue("export/game"); }
        finally { ImGui.EndDisabled(); }
        if (!availability.isVisible || !availability.isEnabled) EditorUiSession.current.widgets.Hint(availability.unavailableReason ?? m_text.Localize("当前宿主未提供可用的 export/game 动作。"));
        if (m_report is null) return;
        ImGui.Text(m_report.title + (m_report.contentValid ? m_text.Localize(" · 内容检查通过") : m_text.Localize(" · 需要修复内容")));
        if (m_report.requiresHostVerification) EditorUiSession.current.widgets.Hint(m_text.Localize("宿主能力或启动配置仍待验证。打开导出窗口不代表这些项目已经完成。"));
        if (ImGui.Button(m_text.Label("保存检查报告")))
        {
            string directory = Path.Combine(context.projectDirectory, "artifacts", "Publishing");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "Preflight-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".md");
            File.WriteAllText(path, m_report.ToMarkdown()); m_message = m_text.Localize("报告已写入：") + path;
        }
        foreach (PublishIssue issue in m_report.issues)
        {
            EditorUiSession.current.widgets.Hint(issue.level + " · " + issue.code + " · " + issue.location + "\n" + issue.message);
        }
        ImGui.Separator();
        ImGui.Text(m_text.Localize("资源闭包：") + m_report.assets.Count + m_text.Localize(" 项。点击可定位资产；属性字节不是发行包体积。"));
        foreach (PublishAsset asset in m_report.assets)
            if (ImGui.Selectable(m_text.Caption(asset.path + " · " + asset.propertyBytes + " 属性字节##" + asset.id, asset.path + " · " + asset.propertyBytes + m_text.Localize(" 属性字节##") + asset.id))
                && Assets.TryLoad<AssetObject>(asset.id, out var selected)) m_interactions.SetSelection(selected);
    }
}

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
[EditorPanel("inno.vn.publishing", "VN Publishing Checks", order: 466, defaultOpen: false, menuPath: "Authoring")]
public sealed class PublishingPanel : EditorPanel
{
    private readonly EditorInteractions m_interactions;
    private NovelPublishReport? m_report;
    private string m_message = string.Empty;
    /// <summary>Binds the engine selection, resource locator and export interaction.</summary>
    /// <param name="interactions">The current editor generation's public action coordinator.</param>
    public PublishingPanel(EditorInteractions interactions) { m_interactions = interactions;  }
    /// <inheritdoc />
    public override Vector2 initialSize => new(920, 720);
    /// <inheritdoc />
    protected override void OnDraw(EditorContext context)
    {
        try { Draw(context); }
        catch (Exception error) when (error is not OutOfMemoryException) { m_message = (error).Message; }
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
    }
    private void Draw(EditorContext context)
    {
        EditorUiSession.current.widgets.Hint("Save all documents, then select an .ivnproject in File Browser. These checks inspect imported content; the engine's Export as Game builds the final game.");
        if (ImGui.Button("Check selected game"))
        {
            NovelProjectAsset project = NovelResourceSelection.Get<NovelProjectAsset>(m_interactions) ?? throw new InvalidOperationException("Select an imported .ivnproject asset.");
            m_report = NovelPublishPreflight.Check(project); m_message = string.Empty;
        }
        var export = m_interactions.For("inno.vn.publishing", m_report);
        EditorActionState availability = export.Query("export/game");
        ImGui.SameLine();
        ImGui.BeginDisabled(m_report?.contentValid != true || !availability.isVisible || !availability.isEnabled);
        try { if (ImGui.Button("Open engine game export")) export.Enqueue("export/game"); }
        finally { ImGui.EndDisabled(); }
        if (!availability.isVisible || !availability.isEnabled) EditorUiSession.current.widgets.Hint(availability.unavailableReason ?? "This host does not provide an available export/game action.");
        if (m_report is null) return;
        ImGui.Text(m_report.title + (m_report.contentValid ? " · Content checks passed" : " · Content needs corrections"));
        if (m_report.requiresHostVerification) EditorUiSession.current.widgets.Hint("Host capabilities or startup configuration still require validation. Opening the export window does not complete these checks.");
        if (ImGui.Button("Save check report"))
        {
            string directory = Path.Combine(context.projectDirectory, "artifacts", "Publishing");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "Preflight-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".md");
            File.WriteAllText(path, m_report.ToMarkdown()); m_message = "Report written: " + path;
        }
        foreach (PublishIssue issue in m_report.issues)
        {
            EditorUiSession.current.widgets.Hint(issue.level + " · " + issue.code + " · " + issue.location + "\n" + issue.message);
        }
        ImGui.Separator();
        ImGui.Text("Resource dependency closure: " + m_report.assets.Count + " entries. Click to locate an asset. Property bytes do not represent package size.");
        foreach (PublishAsset asset in m_report.assets)
            if (ImGui.Selectable(asset.path + " · " + asset.propertyBytes + " property bytes##" + asset.id)
                && Assets.TryLoad<AssetObject>(asset.id, out var selected)) m_interactions.SetSelection(selected);
    }
}

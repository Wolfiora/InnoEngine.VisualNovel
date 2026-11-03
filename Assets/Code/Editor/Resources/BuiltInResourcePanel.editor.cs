using System;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using InnoEditor.Core;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using InnoEditor.Rendering;
using InnoEngine.Assets;
using InnoEngine.Rendering;
using Inno.VisualNovel.Authoring.BuiltInResources;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Editor.Audio;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Editor.ScreenDesigner;
using Inno.VisualNovel.Integration.Rendering;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Editor.Resources;

/// <summary>Browses packaged native presets and previews them through the production presentation chain.</summary>
[EditorPanel("inno.vn.resources", "VN Built-in Resources", order: 459, defaultOpen: false, menuPath: "Authoring")]
public sealed class BuiltInResourcePanel : EditorPanel
{
    private readonly ResourceDocumentsModule m_documents;
    private readonly EditorInteractions m_interactions;
    private readonly StoryInspectorFields m_fields;
    private readonly NovelPreviewCanvas m_canvas;
    private readonly AudioAuditionView m_audio;
    private readonly IEditorPreviewService m_previews;
    private readonly Dictionary<string, BuiltInResourceThumbnailView> m_thumbnails = new(StringComparer.Ordinal);
    private readonly ScreenPreviewDataSource m_data = new();
    private AssetObject? m_asset;
    private BuiltInStagePreview? m_stage;
    private string m_filter = string.Empty, m_search = string.Empty, m_selected = string.Empty, m_role = "title", m_message = string.Empty;
    private double m_time;
    private bool m_playing, m_reducedMotion;
    private int m_page;
    private readonly HashSet<string> m_batchSelection = new(StringComparer.Ordinal);
    private BuiltInResourceCopyBatch? m_batch;
    private int m_batchPage;
    /// <summary>Connects immutable preset metadata to native selection, documents, history and rendering.</summary>
    /// <param name="documents">The shared native resource draft owner.</param>
    /// <param name="interactions">The editor's selection and history coordinator.</param>
    /// <param name="previews">The host-owned offscreen rendering service.</param>
    public BuiltInResourcePanel(
        ResourceDocumentsModule documents,
        EditorInteractions interactions,
        IEditorPreviewService previews
    ) {
        m_documents = documents; m_interactions = interactions; m_fields = new(interactions); m_canvas = new(previews); m_audio = new(m_fields); m_previews = previews;
    }
    /// <inheritdoc />
    public override Vector2 initialSize => new(1140, 760);
    /// <inheritdoc />
    public override bool allowScrolling => false;
    /// <inheritdoc />
    protected override void OnDraw(EditorContext context)
    {
        m_fields.BeginFrame();
        try { Draw(); } catch (Exception error) when (error is not OutOfMemoryException) { m_message = (error).Message; }
        finally { m_fields.EndFrame(); }
    }
    /// <inheritdoc />
    protected override void OnDetach(EditorContext context) { m_batch?.Cancel(); m_batch = null; ClearThumbnails(); m_audio.Dispose(); m_canvas.Dispose(); m_fields.Finish(); m_asset = null; m_stage = null; }
    private void Draw()
    {
        if (ImGui.InputText("Search name, usage or path", ref m_search, 256)) m_page = 0;
        if (m_fields.Select("resources/kind", "Category", ref m_filter, new[] { (string.Empty, "All built-in resources") }
            .Concat(Enum.GetValues<BuiltInResourceKind>().Select(kind => (kind.ToString(), Label(kind)))))) m_page = 0;
        var entries = BuiltInResourceCatalog.entries.Where(entry => (m_filter.Length == 0 || entry.kind.ToString() == m_filter)
            && (entry.name.Contains(m_search, StringComparison.OrdinalIgnoreCase) || entry.path.Contains(m_search, StringComparison.OrdinalIgnoreCase) || entry.usage.Contains(m_search, StringComparison.OrdinalIgnoreCase)
                || entry.name.Contains(m_search, StringComparison.OrdinalIgnoreCase) || entry.usage.Contains(m_search, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (m_selected.Length == 0 && entries.Length > 0) Select(entries[0]);
        m_page = Math.Clamp(m_page, 0, Math.Max(0, (entries.Length - 1) / 12));
        var visible = entries.Skip(m_page * 12).Take(12).ToArray();
        var retained = visible.Select(static entry => entry.path).ToHashSet(StringComparer.Ordinal);
        foreach (string key in m_thumbnails.Keys.Where(key => !retained.Contains(key)).ToArray()) { m_thumbnails[key].Dispose(); m_thumbnails.Remove(key); }
        Child("builtin-list", new(300, 0), () =>
        {
            ImGui.Text(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0} items · Page {1}", entries.Length, m_page + 1));
            ImGui.Text("Batch selection · " + m_batchSelection.Count + " items (retained across pages)");
            if (ImGui.SmallButton("Select editable resources in this filter")) foreach (var entry in entries.Where(static entry => entry.kind != BuiltInResourceKind.Icon)) m_batchSelection.Add(entry.path);
            if (m_batchSelection.Count > 0 && ImGui.SmallButton("Clear batch selection")) m_batchSelection.Clear();
            if (m_batch?.running != true && m_batchSelection.Count > 0 && ImGui.Button("Create editable copies in bulk"))
            {
                m_fields.Finish(); m_batch = new(BuiltInResourceCatalog.entries.Where(entry => m_batchSelection.Contains(entry.path)).Select(BuiltInResourceLoader.Load)); m_batchPage = 0;
            }
            if (ImGui.SmallButton("Refresh thumbnails")) ClearThumbnails();
            if (ImGui.BeginTable("builtin-thumbnails", 2, ImGuiTableFlags.SizingStretchProp))
            {
                try
                {
                    ImGui.TableSetupColumn("Preview", ImGuiTableColumnFlags.WidthStretch, 1);
                    ImGui.TableSetupColumn("Resource", ImGuiTableColumnFlags.WidthStretch, 2);
                    foreach (var entry in visible)
                    {
                        ImGui.TableNextColumn();
                        if (entry.kind != BuiltInResourceKind.Icon)
                        {
                            bool selected = m_batchSelection.Contains(entry.path);
                            if (ImGui.Checkbox("Batch##" + entry.path, ref selected)) { if (selected) m_batchSelection.Add(entry.path); else m_batchSelection.Remove(entry.path); }
                        }
                        if (!m_thumbnails.TryGetValue(entry.path, out var thumbnail)) m_thumbnails.Add(entry.path, thumbnail = new(entry, m_previews));
                        Vector2 position = ImGui.GetCursorScreenPosition(), mouse = ImGui.GetMousePos();
                        float width = Math.Clamp(ImGui.GetContentRegionAvailable().X, 1, 96); Vector2 size = new(width, width * 54 / 96);
                        thumbnail.Draw(size);
                        if (ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && mouse.X >= position.X && mouse.Y >= position.Y
                            && mouse.X < position.X + size.X && mouse.Y < position.Y + size.Y) Select(entry);
                        ImGui.TableNextColumn();
                        if (ImGui.Selectable(entry.kind + " · " + entry.name + "##" + entry.path, entry.path == m_selected)) Select(entry);
                        if (thumbnail.error.Length > 0 && entry.path == m_selected) EditorUiSession.current.widgets.Hint("Thumbnail unavailable: " + thumbnail.error);
                    }
                }
                finally { ImGui.EndTable(); }
            }
            if (m_page > 0 && ImGui.Button("Previous page")) m_page--;
            if ((m_page + 1) * 12 < entries.Length && ImGui.Button("Next page")) m_page++;
        });
        ImGui.SameLine(); Child("builtin-preview", new(0, 0), () => { Batch(); Details(); });
    }
    private void Batch()
    {
        if (m_batch is not { } batch) return;
        ImGui.SeparatorText("Project copy batch · " + batch.completed + "/" + batch.total);
        if (batch.running)
        {
            if (ImGui.Button("Stop batch copy")) batch.Cancel();
            else batch.Advance();
        }
        EditorUiSession.current.widgets.Hint(batch.running ? "Copies one resource per editor update. A theme and its screens count as one item. A native import cannot be interrupted midway."
            : batch.error.Length > 0 ? "Batch stopped: " + (batch.failure is { } cause ? (cause).Message : batch.error) : batch.cancelled ? "Stopped. The created assets below remain in the project." : "Batch copy complete. These are independent project assets.");
        int pages = Math.Max(1, (batch.created.Count + 9) / 10); m_batchPage = Math.Clamp(m_batchPage, 0, pages - 1);
        foreach (var output in batch.created.Skip(m_batchPage * 10).Take(10))
            if (ImGui.Selectable(output.path + "##batch-output/" + output.assetId))
            { var asset = Assets.Load<AssetObject>(output.assetId); m_interactions.SetSelection(asset); m_documents.Open(asset.assetPath); }
        if (m_batchPage > 0 && ImGui.SmallButton("Previous output page")) m_batchPage--;
        if (m_batchPage + 1 < pages && ImGui.SmallButton("Next output page")) m_batchPage++;
    }
    private void Select(BuiltInResourceEntry entry)
    {
        m_fields.Finish(); m_audio.Stop(); m_time = 0; m_playing = false; m_stage = null; m_asset = null; m_selected = entry.path; m_message = string.Empty;
        m_asset = BuiltInResourceLoader.Load(entry);
        if (m_asset is StageAsset or CharacterAsset or PerformanceClipAsset or VisualEffectAsset)
            m_stage = new(m_asset, BuiltInNovelAssets.Load<StageAsset>("BuiltIn/Backgrounds/Classroom.ivnstage"),
                BuiltInNovelAssets.Load<CharacterAsset>("BuiltIn/Characters/Cheng/Cheng.ivncharacter"), BuiltInNovelAssets.Load<StageAsset>("BuiltIn/Backgrounds/Seafront.ivnstage"));
    }
    private void ClearThumbnails() { foreach (var thumbnail in m_thumbnails.Values) thumbnail.Dispose(); m_thumbnails.Clear(); }
    private void Details()
    {
        var entry = BuiltInResourceCatalog.entries.SingleOrDefault(entry => entry.path == m_selected); if (entry is null || m_asset is null) return;
        ImGui.Text(entry.name); EditorUiSession.current.widgets.Hint(entry.usage); ImGui.Text(m_asset.assetPath.ToString());
        if (m_asset is VisualEffectAsset effect) EditorUiSession.current.widgets.Hint(effect.description);
        EditorUiSession.current.widgets.Hint("Original resources: Apache License 2.0. Retain the license and copyright notices. Installed resources are read-only; create a project copy before editing parameters.");
        if (ImGui.Button("Select this resource for other panels")) m_interactions.SetSelection(m_asset);
        if (m_asset is not TextureAsset && ImGui.Button("Create editable project copy"))
        {
            m_fields.Finish(); AssetObject copy = BuiltInResourceCopyService.Copy(m_asset); m_interactions.SetSelection(copy); m_documents.Open(copy.assetPath);
            m_message = "Created independent copy: " + copy.assetPath + ". Continue in the corresponding designer, or link this resource in game settings.";
        }
        if (m_asset is AudioCueAsset cue) { ImGui.Text(cue.author + " · " + cue.clip?.duration); m_audio.Draw(cue); }
        else Preview();
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
    }
    private void Preview()
    {
        float width = Math.Max(1, ImGui.GetContentRegionAvailable().X);
        if (m_stage is not null)
        {
            if (ImGui.Button(m_playing ? "Pause presentation" : "Play presentation")) m_playing = !m_playing;
            ImGui.SameLine(); if (ImGui.Button("Preview from start")) m_time = 0;
            m_fields.Toggle("resources/reduced", "Preview reduced motion", ref m_reducedMotion);
            if (m_playing) m_time = (m_time + Math.Clamp(ImGui.GetInput().deltaTime, 0, .25)) % Math.Max(.001, m_stage.duration);
            m_fields.Number("resources/time", "Logical time (seconds)", ref m_time, 0, 86400);
            m_canvas.Draw(new(1920, 1080, []), new(width, width * 1080 / 1920), m_stage.Evaluate(m_time, m_reducedMotion));
        }
        else if (m_asset is ThemeAsset theme)
        {
            m_fields.Select("resources/theme-role", "Preview screen", ref m_role, theme.screens.Select(static binding => (binding.role, binding.role)));
            var screen = theme.screens.SingleOrDefault(binding => binding.role == m_role)?.screen ?? theme.screens.FirstOrDefault()?.screen;
            if (screen is null) return;
            var page = new UiDocumentInstance(screen, NovelPresentationFrame.Measure, NovelPresentationFrame.MeasureRichText, theme);
            UiFrame frame = page.BuildFrame(m_data); m_canvas.Draw(frame, new(width, width * frame.height / frame.width));
        }
        else if (m_asset is TextureAsset image)
        {
            var page = new ScreenAsset { width = 600, height = 350, root = new() { id = "root", layout = new() { width = 600, height = 350 },
                style = new() { background = 0x243448FF }, children = [new() { id = "image", kind = UiElementKind.Image, image = image, imageFit = UiImageFit.Contain,
                    layout = new() { x = 200, y = 75, width = 200, height = 200 }, style = new() { background = 0 } }] } };
            var instance = new UiDocumentInstance(page, static (_, _) => (0, 0)); m_canvas.Draw(instance.BuildFrame(m_data), new(width, width * 350 / 600));
        }
        else if (m_asset is ScreenAsset widget)
        {
            var instance = new UiDocumentInstance(widget, NovelPresentationFrame.Measure, NovelPresentationFrame.MeasureRichText);
            var frame = instance.BuildFrame(m_data); m_canvas.Draw(frame, new(width, width * frame.height / frame.width));
        }
    }
    private static string Label(BuiltInResourceKind kind) => kind switch
    { BuiltInResourceKind.Widget => "Widget", BuiltInResourceKind.Theme => "Theme", BuiltInResourceKind.Stage => "Background", BuiltInResourceKind.Character => "Character", BuiltInResourceKind.Motion => "Motion", BuiltInResourceKind.Particle => "Particles", BuiltInResourceKind.Transition => "Transition", BuiltInResourceKind.ScreenEffect => "Screen effect", BuiltInResourceKind.Audio => "Audio", BuiltInResourceKind.Icon => "Icons", _ => throw new ArgumentOutOfRangeException(nameof(kind)) };
    private static void Child(
        string id,
        Vector2 size,
        Action draw
    ) {
        bool visible = ImGui.BeginChild(id, size, ImGuiChildFlags.Borders);
        try { if (visible) draw(); } finally { ImGui.EndChild(); }
    }
}

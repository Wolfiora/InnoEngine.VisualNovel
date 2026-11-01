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
[EditorPanel("inno.vn.resources", "VN 内置资源 · Built-in Resources", order: 459, menuPath: "Authoring")]
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
        try { Draw(); } catch (Exception error) when (error is not OutOfMemoryException) { m_message = m_fields.Error(error); }
        finally { m_fields.EndFrame(); }
    }
    /// <inheritdoc />
    protected override void OnDetach(EditorContext context) { m_batch?.Cancel(); m_batch = null; ClearThumbnails(); m_audio.Dispose(); m_canvas.Dispose(); m_fields.Finish(); m_asset = null; m_stage = null; }
    private void Draw()
    {
        if (ImGui.InputText(m_fields.Label("搜索名称、用途或路径"), ref m_search, 256)) m_page = 0;
        if (m_fields.Select("resources/kind", m_fields.Localize("分类"), ref m_filter, new[] { (string.Empty, m_fields.Localize("全部内置资源")) }
            .Concat(Enum.GetValues<BuiltInResourceKind>().Select(kind => (kind.ToString(), m_fields.Localize(Label(kind))))))) m_page = 0;
        var entries = BuiltInResourceCatalog.entries.Where(entry => (m_filter.Length == 0 || entry.kind.ToString() == m_filter)
            && (entry.name.Contains(m_search, StringComparison.OrdinalIgnoreCase) || entry.path.Contains(m_search, StringComparison.OrdinalIgnoreCase) || entry.usage.Contains(m_search, StringComparison.OrdinalIgnoreCase)
                || m_fields.Localize(entry.name).Contains(m_search, StringComparison.OrdinalIgnoreCase) || m_fields.Localize(entry.usage).Contains(m_search, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (m_selected.Length == 0 && entries.Length > 0) Select(entries[0]);
        m_page = Math.Clamp(m_page, 0, Math.Max(0, (entries.Length - 1) / 12));
        var visible = entries.Skip(m_page * 12).Take(12).ToArray();
        var retained = visible.Select(static entry => entry.path).ToHashSet(StringComparer.Ordinal);
        foreach (string key in m_thumbnails.Keys.Where(key => !retained.Contains(key)).ToArray()) { m_thumbnails[key].Dispose(); m_thumbnails.Remove(key); }
        Child("builtin-list", new(300, 0), () =>
        {
            ImGui.Text(m_fields.Format("{0} 项 · 第 {1} 页", entries.Length, m_page + 1));
            ImGui.Text(m_fields.Localize("批量选择 · ") + m_batchSelection.Count + m_fields.Localize(" 项（跨页保留）"));
            if (ImGui.SmallButton(m_fields.Label("选择筛选内可编辑资源"))) foreach (var entry in entries.Where(static entry => entry.kind != BuiltInResourceKind.Icon)) m_batchSelection.Add(entry.path);
            if (m_batchSelection.Count > 0 && ImGui.SmallButton(m_fields.Label("清空批量选择"))) m_batchSelection.Clear();
            if (m_batch?.running != true && m_batchSelection.Count > 0 && ImGui.Button(m_fields.Label("批量创建可编辑副本")))
            {
                m_fields.Finish(); m_batch = new(BuiltInResourceCatalog.entries.Where(entry => m_batchSelection.Contains(entry.path)).Select(BuiltInResourceLoader.Load)); m_batchPage = 0;
            }
            if (ImGui.SmallButton(m_fields.Label("刷新缩略图"))) ClearThumbnails();
            if (ImGui.BeginTable("builtin-thumbnails", 2, ImGuiTableFlags.SizingStretchProp))
            {
                try
                {
                    ImGui.TableSetupColumn(m_fields.Localize("预览"), ImGuiTableColumnFlags.WidthStretch, 1);
                    ImGui.TableSetupColumn(m_fields.Localize("资源"), ImGuiTableColumnFlags.WidthStretch, 2);
                    foreach (var entry in visible)
                    {
                        ImGui.TableNextColumn();
                        if (entry.kind != BuiltInResourceKind.Icon)
                        {
                            bool selected = m_batchSelection.Contains(entry.path);
                            if (ImGui.Checkbox(m_fields.Caption("批量##" + entry.path, m_fields.Localize("批量##") + entry.path), ref selected)) { if (selected) m_batchSelection.Add(entry.path); else m_batchSelection.Remove(entry.path); }
                        }
                        if (!m_thumbnails.TryGetValue(entry.path, out var thumbnail)) m_thumbnails.Add(entry.path, thumbnail = new(entry, m_previews));
                        Vector2 position = ImGui.GetCursorScreenPosition(), mouse = ImGui.GetMousePos();
                        float width = Math.Clamp(ImGui.GetContentRegionAvailable().X, 1, 96); Vector2 size = new(width, width * 54 / 96);
                        thumbnail.Draw(size);
                        if (ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && mouse.X >= position.X && mouse.Y >= position.Y
                            && mouse.X < position.X + size.X && mouse.Y < position.Y + size.Y) Select(entry);
                        ImGui.TableNextColumn();
                        if (ImGui.Selectable(m_fields.Caption(Label(entry.kind) + " · " + entry.name + "##" + entry.path,
                            m_fields.Localize(Label(entry.kind)) + " · " + m_fields.Localize(entry.name) + "##" + entry.path), entry.path == m_selected)) Select(entry);
                        if (thumbnail.error.Length > 0 && entry.path == m_selected) EditorUiSession.current.widgets.Hint(m_fields.Localize("缩略图不可用：") + thumbnail.error);
                    }
                }
                finally { ImGui.EndTable(); }
            }
            if (m_page > 0 && ImGui.Button(m_fields.Label("上一页"))) m_page--;
            if ((m_page + 1) * 12 < entries.Length && ImGui.Button(m_fields.Label("下一页"))) m_page++;
        });
        ImGui.SameLine(); Child("builtin-preview", new(0, 0), () => { Batch(); Details(); });
    }
    private void Batch()
    {
        if (m_batch is not { } batch) return;
        ImGui.SeparatorText(m_fields.Localize("项目副本批次 · ") + batch.completed + "/" + batch.total);
        if (batch.running)
        {
            if (ImGui.Button(m_fields.Label("停止批量复制"))) batch.Cancel();
            else batch.Advance();
        }
        EditorUiSession.current.widgets.Hint(batch.running ? m_fields.Localize("每次编辑器更新复制一个资源。一个主题连同它的页面作为一项；原生导入不可中途抢占。")
            : batch.error.Length > 0 ? m_fields.Localize("批次已停止：") + (batch.failure is { } cause ? m_fields.Error(cause) : batch.error) : batch.cancelled ? m_fields.Localize("已停止。以下已创建资源保留在项目中。") : m_fields.Localize("批量复制完成。以下均为独立项目资产。"));
        int pages = Math.Max(1, (batch.created.Count + 9) / 10); m_batchPage = Math.Clamp(m_batchPage, 0, pages - 1);
        foreach (var output in batch.created.Skip(m_batchPage * 10).Take(10))
            if (ImGui.Selectable(output.path + "##batch-output/" + output.assetId))
            { var asset = Assets.Load<AssetObject>(output.assetId); m_interactions.SetSelection(asset); m_documents.Open(asset.assetPath); }
        if (m_batchPage > 0 && ImGui.SmallButton(m_fields.Label("上一页副本"))) m_batchPage--;
        if (m_batchPage + 1 < pages && ImGui.SmallButton(m_fields.Label("下一页副本"))) m_batchPage++;
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
        ImGui.Text(m_fields.Localize(entry.name)); EditorUiSession.current.widgets.Hint(m_fields.Localize(entry.usage)); ImGui.Text(m_asset.assetPath.ToString());
        if (m_asset is VisualEffectAsset effect) EditorUiSession.current.widgets.Hint(effect.description);
        EditorUiSession.current.widgets.Hint(m_fields.Localize("原创资源：Apache License 2.0；保留许可与版权说明。安装资源只读，参数修改先创建项目副本。"));
        if (ImGui.Button(m_fields.Label("选择此资源供其他面板使用"))) m_interactions.SetSelection(m_asset);
        if (m_asset is not TextureAsset && ImGui.Button(m_fields.Label("创建可编辑项目副本")))
        {
            m_fields.Finish(); AssetObject copy = BuiltInResourceCopyService.Copy(m_asset); m_interactions.SetSelection(copy); m_documents.Open(copy.assetPath);
            m_message = m_fields.Localize("已创建独立副本：") + copy.assetPath + m_fields.Localize("。在对应设计器继续编辑；游戏设置可关联这个资源。");
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
            if (ImGui.Button(m_fields.Caption(m_playing ? "暂停演出" : "播放演出", m_playing ? m_fields.Localize("暂停演出") : m_fields.Localize("播放演出")))) m_playing = !m_playing;
            ImGui.SameLine(); if (ImGui.Button(m_fields.Label("从头预览"))) m_time = 0;
            m_fields.Toggle("resources/reduced", "检查减少动态效果", ref m_reducedMotion);
            if (m_playing) m_time = (m_time + Math.Clamp(ImGui.GetInput().deltaTime, 0, .25)) % Math.Max(.001, m_stage.duration);
            m_fields.Number("resources/time", m_fields.Localize("逻辑时间（秒）"), ref m_time, 0, 86400);
            m_canvas.Draw(new(1920, 1080, []), new(width, width * 1080 / 1920), m_stage.Evaluate(m_time, m_reducedMotion));
        }
        else if (m_asset is ThemeAsset theme)
        {
            m_fields.Select("resources/theme-role", m_fields.Localize("预览页面"), ref m_role, theme.screens.Select(static binding => (binding.role, binding.role)));
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
    { BuiltInResourceKind.Widget => "组件", BuiltInResourceKind.Theme => "主题", BuiltInResourceKind.Stage => "背景", BuiltInResourceKind.Character => "角色", BuiltInResourceKind.Motion => "动作", BuiltInResourceKind.Particle => "粒子", BuiltInResourceKind.Transition => "转场", BuiltInResourceKind.ScreenEffect => "屏幕效果", BuiltInResourceKind.Audio => "音频", BuiltInResourceKind.Icon => "图标", _ => throw new ArgumentOutOfRangeException(nameof(kind)) };
    private static void Child(
        string id,
        Vector2 size,
        Action draw
    ) {
        bool visible = ImGui.BeginChild(id, size, ImGuiChildFlags.Borders);
        try { if (visible) draw(); } finally { ImGui.EndChild(); }
    }
}

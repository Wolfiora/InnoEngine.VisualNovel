using System;
using System.Linq;
using System.Numerics;
using InnoEditor.Core;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using InnoEditor.Rendering;
using InnoEngine.Text;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Authoring.ThemeEditing;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Editor.ScreenDesigner;
using Inno.VisualNovel.Integration.Rendering;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.UI.Documents;
using Inno.VisualNovel.UI.Theming;

namespace Inno.VisualNovel.Editor.ThemeDesigner;

/// <summary>Edits native theme tokens, inheritance and page mappings through the shared document history.</summary>
[EditorPanel("inno.vn.theme-designer", "VN 主题设计 · Theme Designer", order: 452, menuPath: "Authoring")]
public sealed class ThemeDesignerPanel : EditorPanel, IEditorPanelReloadState
{
    private readonly ResourceDocumentsModule m_documents;
    private readonly EditorInteractions m_interactions;
    private readonly ResourceDocumentToolbar m_toolbar;
    private readonly StoryInspectorFields m_fields;
    private readonly ThemeStyleFields m_styles;
    private readonly NovelPreviewCanvas m_preview;
    private readonly ScreenPreviewDataSource m_data = new();
    private string m_style = string.Empty, m_role = "reading", m_message = string.Empty;
    private double m_scale = 1;
    /// <summary>Connects native resource drafts, engine history and production rendering previews.</summary>
    /// <param name="documents">The owner of native resource documents.</param>
    /// <param name="interactions">The current editor selection and history.</param>
    /// <param name="previews">The host's offscreen rendering service.</param>
    public ThemeDesignerPanel(
        ResourceDocumentsModule documents,
        EditorInteractions interactions,
        IEditorPreviewService previews
    ) {
        m_documents = documents; m_interactions = interactions; m_fields = new(interactions); m_styles = new(m_fields);
        m_toolbar = new(documents, interactions, m_fields.Finish); m_preview = new(previews);
    }
    /// <inheritdoc />
    public override Vector2 initialSize => new(1040, 800);
    /// <inheritdoc />
    protected override void OnDraw(EditorContext context)
    {
        m_fields.BeginFrame();
        try { Draw(); } catch (Exception error) when (error is not OutOfMemoryException) { m_message = m_fields.Error(error); }
        finally { m_fields.EndFrame(); }
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
    }
    /// <inheritdoc />
    public ReadOnlyMemory<byte> CaptureReloadState() { m_fields.Finish(); return m_documents.CaptureReload(); }
    /// <inheritdoc />
    public void RestoreReloadState(ReadOnlyMemory<byte> state) => m_documents.RestoreReload(state.Span);
    /// <inheritdoc />
    protected override void OnDetach(EditorContext context) { m_fields.Finish(); m_preview.Dispose(); }

    private void Draw()
    {
        if (!m_toolbar.Draw("主题", ".ivntheme", static () => new ThemeAsset { styles = [new() { key = "body", fields = UiStyleProperty.All }] }, out ThemeAsset? theme) || theme is null) return;
        bool changed = false; string title = theme.title, identity = theme.resourceId, description = theme.description;
        changed |= m_fields.Text("theme/id", m_fields.Localize("剧情资源 ID"), ref identity); theme.resourceId = identity;
        changed |= m_fields.Text("theme/title", m_fields.Localize("名称"), ref title); theme.title = title;
        changed |= m_fields.MultilineText("theme/description", m_fields.Localize("用途说明"), ref description); theme.description = description;
        if (NovelResourceSelection.Get<FontAsset>(m_interactions) is { } font && ImGui.Button(m_fields.Label("使用所选默认字体"))) { m_fields.Finish(); theme.font = font; changed = true; }
        if (theme.font is not null && ImGui.Button(m_fields.Label("使用页面自身字体"))) { m_fields.Finish(); theme.font = null; changed = true; }
        if (ImGui.CollapsingHeader(m_fields.Label("颜色 token")))
        {
            for (int index = 0; index < theme.colors.Length; index++)
            {
                var token = theme.colors[index]; string key = token.key; uint color = token.value;
                if (m_fields.Text("theme/color-key/" + index, m_fields.Localize("稳定键"), ref key)) { ThemeEditService.RenameToken(theme, token.key, key); changed = true; }
                changed |= m_fields.Color("theme/color/" + index, key, ref color); token.value = color;
                if (ImGui.SmallButton(m_fields.Caption("移除##color-" + index, m_fields.Localize("移除##color-") + index))) { m_fields.Finish(); ThemeEditService.RemoveToken(theme, key); changed = true; break; }
            }
            if (ImGui.Button(m_fields.Label("添加颜色 token"))) { m_fields.Finish(); ThemeEditService.AddToken(theme, true); changed = true; }
        }
        if (ImGui.CollapsingHeader(m_fields.Label("尺寸 token")))
        {
            for (int index = 0; index < theme.metrics.Length; index++)
            {
                var token = theme.metrics[index]; string key = token.key; double value = token.value;
                if (m_fields.Text("theme/metric-key/" + index, m_fields.Localize("稳定键"), ref key)) { ThemeEditService.RenameToken(theme, token.key, key); changed = true; }
                changed |= m_fields.Number("theme/metric/" + index, key, ref value, 0, 100000); token.value = (float)value;
                if (ImGui.SmallButton(m_fields.Caption("移除##metric-" + index, m_fields.Localize("移除##metric-") + index))) { m_fields.Finish(); ThemeEditService.RemoveToken(theme, key); changed = true; break; }
            }
            if (ImGui.Button(m_fields.Label("添加尺寸 token"))) { m_fields.Finish(); ThemeEditService.AddToken(theme, false); changed = true; }
        }
        if (ImGui.CollapsingHeader(m_fields.Label("继承样式与控件状态")))
        {
            if (!theme.styles.Any(style => style.key == m_style)) m_style = theme.styles.FirstOrDefault()?.key ?? string.Empty;
            m_fields.Select("theme/style", m_fields.Localize("编辑样式"), ref m_style, theme.styles.Select(static style => (style.key, style.key)));
            if (ImGui.Button(m_fields.Label("添加样式"))) { m_fields.Finish(); m_style = ThemeEditService.AddStyle(theme); changed = true; }
            if (theme.styles.SingleOrDefault(style => style.key == m_style) is { } selected) changed |= m_styles.Draw(theme, selected);
            EditorUiSession.current.widgets.Hint(m_fields.Localize("样式键是页面引用身份。继承字段可单独覆盖；token 重命名会更新本主题内的绑定。发布检查验证所有主题页面。"));
        }
        if (ImGui.CollapsingHeader(m_fields.Label("页面映射")))
        {
            m_fields.Select("theme/page-role", m_fields.Localize("系统页面"), ref m_role, new[] { ("loading", m_fields.Localize("启动与加载")) }.Concat(m_fields.BuiltIn(BuiltInUiChoices.Pages)));
            foreach (var binding in theme.screens) ImGui.Text(binding.role + " → " + binding.screen?.name);
            if (NovelResourceSelection.Get<ScreenAsset>(m_interactions) is { } screen && ImGui.Button(m_fields.Label("将所选页面用于此角色")))
            { m_fields.Finish(); theme.screens = [.. theme.screens.Where(binding => binding.role != m_role), new() { role = m_role, screen = screen }]; changed = true; }
            if (theme.screens.Any(binding => binding.role == m_role) && ImGui.Button(m_fields.Label("恢复游戏默认页面")))
            { m_fields.Finish(); theme.screens = theme.screens.Where(binding => binding.role != m_role).ToArray(); changed = true; }
        }
        if (changed) m_documents.Edit<ThemeAsset>(m_toolbar.documentId, m_fields.Localize("编辑主题"), candidate =>
        { candidate.resourceId = theme.resourceId; candidate.title = theme.title; candidate.description = theme.description; candidate.font = theme.font;
            candidate.colors = theme.colors; candidate.metrics = theme.metrics; candidate.styles = theme.styles; candidate.screens = theme.screens; }, m_fields.mergeKey);
        Preview(theme);
    }

    private void Preview(ThemeAsset theme)
    {
        m_fields.Number("theme/preview-scale", m_fields.Localize("预览文字缩放"), ref m_scale, .75, 2);
        var resolver = new UiThemeResolver(theme);
        ScreenAsset screen = theme.screens.SingleOrDefault(binding => binding.role == m_role)?.screen ?? Sample(theme);
        screen = resolver.Apply(screen); screen.font ??= BuiltInNovelAssets.Load<FontAsset>("BuiltIn/Fonts/NotoSansCJKsc-Regular.otf");
        var page = new UiDocumentInstance(screen, NovelPresentationFrame.Measure, NovelPresentationFrame.MeasureRichText); page.SetFontScale((float)m_scale);
        UiFrame frame = page.BuildFrame(m_data);
        float width = Math.Max(1, ImGui.GetContentRegionAvailable().X);
        m_preview.Draw(frame, new(width, width * frame.height / frame.width));
        m_message = string.Empty;
    }
    private ScreenAsset Sample(ThemeAsset theme) => new()
    {
        width = 1000, height = 280, font = theme.font, root = new() { id = "root", kind = UiElementKind.Vertical, blocksPointer = false,
            layout = new() { width = 1000, height = 280, padding = 20, gap = 20 }, style = new() { background = 0x171C25FF }, children = [
                new() { id = "normal", kind = UiElementKind.Button, text = "主题 · 普通与键盘焦点", styleClass = m_style, layout = new() { widthMode = UiSizeMode.Fill, height = 100 } },
                new() { id = "disabled", kind = UiElementKind.Button, interactable = false, text = "主题 · 禁用状态", styleClass = m_style, layout = new() { widthMode = UiSizeMode.Fill, height = 100 } }] }
    };
}

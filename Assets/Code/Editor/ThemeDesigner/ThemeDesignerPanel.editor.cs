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
[EditorPanel("inno.vn.theme-designer", "VN Theme Designer", order: 452, defaultOpen: false, menuPath: "Authoring")]
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
        try { Draw(); } catch (Exception error) when (error is not OutOfMemoryException) { m_message = (error).Message; }
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
        if (!m_toolbar.Draw("Theme", ".ivntheme", static () => new ThemeAsset { styles = [new() { key = "body", fields = UiStyleProperty.All }] }, out ThemeAsset? theme) || theme is null) return;
        bool changed = false; string title = theme.title, identity = theme.resourceId, description = theme.description;
        changed |= m_fields.Text("theme/id", "Story resource ID", ref identity); theme.resourceId = identity;
        changed |= m_fields.Text("theme/title", "Name", ref title); theme.title = title;
        changed |= m_fields.MultilineText("theme/description", "Purpose", ref description); theme.description = description;
        if (NovelResourceSelection.Get<FontAsset>(m_interactions) is { } font && ImGui.Button("Use selected default font")) { m_fields.Finish(); theme.font = font; changed = true; }
        if (theme.font is not null && ImGui.Button("Use each screen's font")) { m_fields.Finish(); theme.font = null; changed = true; }
        if (ImGui.CollapsingHeader("Color tokens"))
        {
            for (int index = 0; index < theme.colors.Length; index++)
            {
                var token = theme.colors[index]; string key = token.key; uint color = token.value;
                if (m_fields.Text("theme/color-key/" + index, "Stable key", ref key)) { ThemeEditService.RenameToken(theme, token.key, key); changed = true; }
                changed |= m_fields.Color("theme/color/" + index, key, ref color); token.value = color;
                if (ImGui.SmallButton("Remove##color-" + index)) { m_fields.Finish(); ThemeEditService.RemoveToken(theme, key); changed = true; break; }
            }
            if (ImGui.Button("Add color token")) { m_fields.Finish(); ThemeEditService.AddToken(theme, true); changed = true; }
        }
        if (ImGui.CollapsingHeader("Size tokens"))
        {
            for (int index = 0; index < theme.metrics.Length; index++)
            {
                var token = theme.metrics[index]; string key = token.key; double value = token.value;
                if (m_fields.Text("theme/metric-key/" + index, "Stable key", ref key)) { ThemeEditService.RenameToken(theme, token.key, key); changed = true; }
                changed |= m_fields.Number("theme/metric/" + index, key, ref value, 0, 100000); token.value = (float)value;
                if (ImGui.SmallButton("Remove##metric-" + index)) { m_fields.Finish(); ThemeEditService.RemoveToken(theme, key); changed = true; break; }
            }
            if (ImGui.Button("Add size token")) { m_fields.Finish(); ThemeEditService.AddToken(theme, false); changed = true; }
        }
        if (ImGui.CollapsingHeader("Style inheritance and control states"))
        {
            if (!theme.styles.Any(style => style.key == m_style)) m_style = theme.styles.FirstOrDefault()?.key ?? string.Empty;
            m_fields.Select("theme/style", "Edit style", ref m_style, theme.styles.Select(static style => (style.key, style.key)));
            if (ImGui.Button("Add style")) { m_fields.Finish(); m_style = ThemeEditService.AddStyle(theme); changed = true; }
            if (theme.styles.SingleOrDefault(style => style.key == m_style) is { } selected) changed |= m_styles.Draw(theme, selected);
            EditorUiSession.current.widgets.Hint("Style keys identify screen references. Inherited fields can be overridden individually; renaming a token updates bindings in this theme. Publishing checks validate all themed screens.");
        }
        if (ImGui.CollapsingHeader("Screen mappings"))
        {
            m_fields.Select("theme/page-role", "System screen", ref m_role, new[] { ("loading", "Startup and loading") }.Concat(BuiltInUiChoices.Pages));
            foreach (var binding in theme.screens) ImGui.Text(binding.role + " → " + binding.screen?.name);
            if (NovelResourceSelection.Get<ScreenAsset>(m_interactions) is { } screen && ImGui.Button("Use selected screen for this role"))
            { m_fields.Finish(); theme.screens = [.. theme.screens.Where(binding => binding.role != m_role), new() { role = m_role, screen = screen }]; changed = true; }
            if (theme.screens.Any(binding => binding.role == m_role) && ImGui.Button("Restore game default screen"))
            { m_fields.Finish(); theme.screens = theme.screens.Where(binding => binding.role != m_role).ToArray(); changed = true; }
        }
        if (changed) m_documents.Edit<ThemeAsset>(m_toolbar.documentId, "Edit theme", candidate =>
        { candidate.resourceId = theme.resourceId; candidate.title = theme.title; candidate.description = theme.description; candidate.font = theme.font;
            candidate.colors = theme.colors; candidate.metrics = theme.metrics; candidate.styles = theme.styles; candidate.screens = theme.screens; }, m_fields.mergeKey);
        Preview(theme);
    }

    private void Preview(ThemeAsset theme)
    {
        m_fields.Number("theme/preview-scale", "Preview text scale", ref m_scale, .75, 2);
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
                new() { id = "normal", kind = UiElementKind.Button, text = "Theme · Normal and keyboard focus", styleClass = m_style, layout = new() { widthMode = UiSizeMode.Fill, height = 100 } },
                new() { id = "disabled", kind = UiElementKind.Button, interactable = false, text = "Theme · Disabled state", styleClass = m_style, layout = new() { widthMode = UiSizeMode.Fill, height = 100 } }] }
    };
}

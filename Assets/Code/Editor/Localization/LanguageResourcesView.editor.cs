using System;
using System.Linq;
using InnoEngine.Rendering;
using InnoEngine.Assets;
using InnoEngine.Text;
using InnoEditor.ImGui;
using InnoEditor.Core;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.LocalizationEditing;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Localization;

namespace Inno.VisualNovel.Editor.Localization;

internal sealed class LanguageResourcesView(
    ResourceDocumentsModule documents,
    StoryInspectorFields fields
)
{
    private string m_language = "zh-CN";
    private TextureAsset? m_source;
    private int m_page;
    internal void Draw(
        LocalizationTableAsset table,
        Guid documentId,
        EditorInteractions interactions
    ) {
        fields.Select("locale/resources/language", "Language", ref m_language, table.languages.Select(static item => (item.id, item.name)));
        var language = table.languages.SingleOrDefault(item => item.id == m_language);
        if (language is null) { EditorUiSession.current.widgets.Hint("Choose a declared language."); return; }
        EditorUiSession.current.widgets.Hint("The language's default font applies to ordinary controls. Named rich-text fonts are retained, with missing glyphs resolved through the fallback chain. Image overrides use native references: the stage retains its composition size, while UI adapts to the replacement image. Running sessions retain frozen declarations; restart preview after resource changes.");
        ImGui.Text("Default font · " + (language.font?.name ?? "Use theme / screen font"));
        if (ImGui.Button("Use selected font as this language's default"))
        { var font = SelectedFont(interactions); Edit("Language default font", candidate => candidate.font = font); }
        ImGui.SameLine(); if (ImGui.Button("Clear language default font")) Edit("Clear language default font", static candidate => candidate.font = null);
        if (ImGui.Button("Add selected font to fallback chain"))
        { var font = SelectedFont(interactions); Edit("Add fallback font", candidate => candidate.fallbackFonts = [.. candidate.fallbackFonts, font]); }
        for (int index = 0; index < language.fallbackFonts.Length; index++)
        {
            int position = index; ImGui.Text((index + 1) + " · " + language.fallbackFonts[index].name); ImGui.SameLine();
            if (index > 0 && ImGui.SmallButton("Move up##fallback/" + index)) Edit("Reorder fallback fonts", candidate => (candidate.fallbackFonts[position - 1], candidate.fallbackFonts[position]) = (candidate.fallbackFonts[position], candidate.fallbackFonts[position - 1]));
            ImGui.SameLine(); if (ImGui.SmallButton("Remove##fallback/" + index)) Edit("Remove fallback font", candidate => candidate.fallbackFonts = candidate.fallbackFonts.Where((_, offset) => offset != position).ToArray());
        }
        ImGui.Separator();
        if (ImGui.Button("Use selected image as original")) m_source = NovelResourceSelection.Get<TextureAsset>(interactions) ?? throw new InvalidOperationException("Select the imported original image first.");
        ImGui.Text("Original image to link · " + (m_source?.name ?? "Not selected"));
        if (ImGui.Button("Link selected image as this language's version"))
        {
            var original = m_source ?? throw new InvalidOperationException("Choose the original image, then the localized image.");
            var image = NovelResourceSelection.Get<TextureAsset>(interactions) ?? throw new InvalidOperationException("Select an imported localized image.");
            Edit("Link localized image", candidate => candidate.images = [.. candidate.images.Where(item => !item.Matches(original)), new() { source = original, image = image }]);
        }
        int pages = Math.Max(1, (language.images.Length + 19) / 20); m_page = Math.Clamp(m_page, 0, pages - 1);
        if (ImGui.Button("Previous page##language-images")) m_page = Math.Max(0, m_page - 1);
        ImGui.SameLine(); ImGui.Text((m_page + 1) + " / " + pages); ImGui.SameLine();
        if (ImGui.Button("Next page##language-images")) m_page = Math.Min(pages - 1, m_page + 1);
        foreach (var mapping in language.images.Skip(m_page * 20).Take(20))
        {
            ImGui.Text((mapping.source?.name ?? "Missing") + " → " + (mapping.image?.name ?? "Missing")); ImGui.SameLine();
            if (ImGui.SmallButton("Remove##localized-image/" + mapping.source?.identity.persistentId))
                Edit("Remove localized image", candidate => candidate.images = candidate.images.Where(item => mapping.source is null || !item.Matches(mapping.source)).ToArray());
        }
        void Edit(
            string label,
            Action<LanguageDefinition> change
        ) {
            fields.Finish(); documents.Edit<LocalizationTableAsset>(documentId, label, candidate =>
            { var copy = candidate.languages.Single(item => item.id == language.id).Copy(); change(copy); LocalizationEditService.SetPresentation(candidate, copy); });
        }
    }
    private static FontAsset SelectedFont(EditorInteractions interactions) => NovelResourceSelection.Get<FontAsset>(interactions) ?? throw new InvalidOperationException("Select an imported TTF/OTF font in File Browser.");
}

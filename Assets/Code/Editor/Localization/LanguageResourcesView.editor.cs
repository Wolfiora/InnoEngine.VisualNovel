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
        fields.Select("locale/resources/language", fields.Localize("语言"), ref m_language, table.languages.Select(static item => (item.id, item.name)));
        var language = table.languages.SingleOrDefault(item => item.id == m_language);
        if (language is null) { EditorUiSession.current.widgets.Hint(fields.Localize("选择已声明的语言。")); return; }
        EditorUiSession.current.widgets.Hint(fields.Localize("语言默认字体用于普通控件；命名富文本字体保留，缺字按链回退。图像替换使用原生引用，舞台保留原构图尺寸，UI 按替换图重新适配。运行中的会话持有冻结声明，资源更改后重新预览。"));
        ImGui.Text(fields.Localize("默认字体 · ") + (language.font?.name ?? fields.Localize("使用主题/页面字体")));
        if (ImGui.Button(fields.Label("使用所选字体为本语言默认")))
        { var font = SelectedFont(interactions); Edit("语言默认字体", candidate => candidate.font = font); }
        ImGui.SameLine(); if (ImGui.Button(fields.Label("清除语言默认字体"))) Edit("清除语言默认字体", static candidate => candidate.font = null);
        if (ImGui.Button(fields.Label("添加所选字体到回退链")))
        { var font = SelectedFont(interactions); Edit("添加回退字体", candidate => candidate.fallbackFonts = [.. candidate.fallbackFonts, font]); }
        for (int index = 0; index < language.fallbackFonts.Length; index++)
        {
            int position = index; ImGui.Text((index + 1) + " · " + language.fallbackFonts[index].name); ImGui.SameLine();
            if (index > 0 && ImGui.SmallButton(fields.Caption("上移##fallback/" + index, fields.Localize("上移##fallback/") + index))) Edit("重排回退字体", candidate => (candidate.fallbackFonts[position - 1], candidate.fallbackFonts[position]) = (candidate.fallbackFonts[position], candidate.fallbackFonts[position - 1]));
            ImGui.SameLine(); if (ImGui.SmallButton(fields.Caption("移除##fallback/" + index, fields.Localize("移除##fallback/") + index))) Edit("移除回退字体", candidate => candidate.fallbackFonts = candidate.fallbackFonts.Where((_, offset) => offset != position).ToArray());
        }
        ImGui.Separator();
        if (ImGui.Button(fields.Label("将所选图片设为原图"))) m_source = NovelResourceSelection.Get<TextureAsset>(interactions) ?? throw new InvalidOperationException(fields.Localize("先选择已导入的原始图片。"));
        ImGui.Text(fields.Localize("待关联原图 · ") + (m_source?.name ?? fields.Localize("未选择")));
        if (ImGui.Button(fields.Label("将所选图片关联为本语言版本")))
        {
            var original = m_source ?? throw new InvalidOperationException(fields.Localize("先选择原图，再选择翻译后的图片。"));
            var image = NovelResourceSelection.Get<TextureAsset>(interactions) ?? throw new InvalidOperationException(fields.Localize("选择已导入的语言图片。"));
            Edit("关联语言图片", candidate => candidate.images = [.. candidate.images.Where(item => !item.Matches(original)), new() { source = original, image = image }]);
        }
        int pages = Math.Max(1, (language.images.Length + 19) / 20); m_page = Math.Clamp(m_page, 0, pages - 1);
        if (ImGui.Button(fields.Label("上一页##language-images"))) m_page = Math.Max(0, m_page - 1);
        ImGui.SameLine(); ImGui.Text((m_page + 1) + " / " + pages); ImGui.SameLine();
        if (ImGui.Button(fields.Label("下一页##language-images"))) m_page = Math.Min(pages - 1, m_page + 1);
        foreach (var mapping in language.images.Skip(m_page * 20).Take(20))
        {
            ImGui.Text((mapping.source?.name ?? "Missing") + " → " + (mapping.image?.name ?? "Missing")); ImGui.SameLine();
            if (ImGui.SmallButton(fields.Caption("移除##localized-image/" + mapping.source?.identity.persistentId, fields.Localize("移除##localized-image/") + mapping.source?.identity.persistentId)))
                Edit("移除语言图片", candidate => candidate.images = candidate.images.Where(item => mapping.source is null || !item.Matches(mapping.source)).ToArray());
        }
        void Edit(
            string label,
            Action<LanguageDefinition> change
        ) {
            fields.Finish(); documents.Edit<LocalizationTableAsset>(documentId, label, candidate =>
            { var copy = candidate.languages.Single(item => item.id == language.id).Copy(); change(copy); LocalizationEditService.SetPresentation(candidate, copy); });
        }
    }
    private static FontAsset SelectedFont(EditorInteractions interactions) => NovelResourceSelection.Get<FontAsset>(interactions) ?? throw new InvalidOperationException("在 File Browser 选择已导入的 TTF/OTF 字体。");
}

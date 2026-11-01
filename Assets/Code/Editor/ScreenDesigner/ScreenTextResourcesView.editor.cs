using System;
using System.Linq;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using InnoEngine.Text;
using InnoEngine.Rendering;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Editor.Presentation;

namespace Inno.VisualNovel.Editor.ScreenDesigner;

internal sealed class ScreenTextResourcesView(StoryInspectorFields fields)
{
    private string m_error = string.Empty;
    internal bool Draw(
        ScreenAsset screen,
        EditorInteractions interactions
    ) {
        if (!ImGui.CollapsingHeader(fields.Label("页面富文本资源"))) return false;
        EditorUiSession.current.widgets.Hint(fields.Localize("为字体与行内图像命名。正文的字体/图片资源键引用这里的原生资源；插件打包沿这些引用收集依赖。"));
        bool changed = false;
        foreach (UiTextFont item in screen.textFonts)
        {
            string key = item.key;
            if (fields.Text("text-font/" + item.key, fields.Localize("字体资源键"), ref key))
            {
                if (Valid(key) && screen.textFonts.All(other => ReferenceEquals(other, item) || other.key != key)) { item.key = key; changed = true; m_error = string.Empty; }
                else m_error = fields.Localize("资源键需要唯一，且仅包含字母、数字、_、-、.、/。");
            }
            ImGui.Text(item.font is null ? fields.Localize("Missing · 尚未绑定字体") : fields.Localize("已绑定字体 · ") + item.font.identity.persistentId);
            if (NovelResourceSelection.Get<FontAsset>(interactions) is { } font && ImGui.Button(fields.Caption("绑定所选字体##" + item.key, fields.Localize("绑定所选字体##") + item.key))) { fields.Finish(); item.font = font; changed = true; }
            if (ImGui.Button(fields.Caption("移除此字体绑定##" + item.key, fields.Localize("移除此字体绑定##") + item.key))) { fields.Finish(); screen.textFonts = screen.textFonts.Where(other => !ReferenceEquals(item, other)).ToArray(); changed = true; break; }
        }
        if (ImGui.Button(fields.Label("添加字体绑定")))
        { fields.Finish(); screen.textFonts = [.. screen.textFonts, new() { key = "font-" + Guid.NewGuid().ToString("N")[..8], font = NovelResourceSelection.Get<FontAsset>(interactions) }]; changed = true; }
        foreach (UiTextIcon item in screen.textIcons)
        {
            string key = item.key;
            if (fields.Text("text-icon/" + item.key, fields.Localize("行内图片资源键"), ref key))
            {
                if (Valid(key) && screen.textIcons.All(other => ReferenceEquals(other, item) || other.key != key)) { item.key = key; changed = true; m_error = string.Empty; }
                else m_error = fields.Localize("资源键需要唯一，且仅包含字母、数字、_、-、.、/。");
            }
            ImGui.Text(item.image is null ? fields.Localize("Missing · 尚未绑定图片") : fields.Localize("已绑定图片 · ") + item.image.identity.persistentId);
            if (NovelResourceSelection.Get<TextureAsset>(interactions) is { } image && ImGui.Button(fields.Caption("绑定所选行内图像##" + item.key, fields.Localize("绑定所选行内图像##") + item.key))) { fields.Finish(); item.image = image; changed = true; }
            if (ImGui.Button(fields.Caption("移除此图片绑定##" + item.key, fields.Localize("移除此图片绑定##") + item.key))) { fields.Finish(); screen.textIcons = screen.textIcons.Where(other => !ReferenceEquals(item, other)).ToArray(); changed = true; break; }
        }
        if (ImGui.Button(fields.Label("添加行内图片绑定")))
        { fields.Finish(); screen.textIcons = [.. screen.textIcons, new() { key = "icon-" + Guid.NewGuid().ToString("N")[..8], image = NovelResourceSelection.Get<TextureAsset>(interactions) }]; changed = true; }
        if (m_error.Length > 0) EditorUiSession.current.widgets.Hint(m_error);
        return changed;
    }
    private static bool Valid(string key) => Inno.VisualNovel.Model.Text.NovelTextMarkup.IsResourceKey(key);
}

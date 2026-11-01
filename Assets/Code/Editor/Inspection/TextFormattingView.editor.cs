using System;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Model.Text;
using Inno.VisualNovel.Authoring.StoryEditing;

namespace Inno.VisualNovel.Editor.Inspection;

internal sealed class TextFormattingView(StoryInspectorFields fields)
{
    private double m_first, m_count = 1, m_size, m_outline, m_shadow, m_pause = 0.3, m_speed = 1;
    private bool m_bold, m_italic, m_colorEnabled;
    private uint m_color = 0xFFFFFFFF, m_outlineColor = 0x000000FF, m_shadowColor = 0x000000A0;
    private string m_font = string.Empty, m_icon = string.Empty, m_ruby = string.Empty, m_error = string.Empty;

    internal bool Draw(
        string id,
        ref string source
    ) {
        if (!ImGui.CollapsingHeader(fields.Caption("文字格式与节奏##" + id, fields.Localize("文字格式与节奏##") + id))) return false;
        NovelTextDocument document;
        try { document = NovelTextMarkup.Parse(source); }
        catch (FormatException failure) { EditorUiSession.current.widgets.Hint(fields.Error(failure)); return false; }
        EditorUiSession.current.widgets.Hint(fields.Localize("正文预览 · ") + document.text);
        fields.Number(id + "/format-first", fields.Localize("起始字素（从 0 开始）"), ref m_first, 0, Math.Max(0, document.elements.Count - 1));
        fields.Number(id + "/format-count", fields.Localize("选择字素数"), ref m_count, 1, Math.Max(1, document.elements.Count - (int)m_first));
        fields.Toggle(id + "/bold", "粗体", ref m_bold); fields.Toggle(id + "/italic", "斜体", ref m_italic);
        fields.Number(id + "/format-size", fields.Localize("字号（0 继承页面）"), ref m_size, 0, 512);
        fields.Toggle(id + "/format-color-enabled", "覆盖文字颜色", ref m_colorEnabled);
        if (m_colorEnabled) fields.Color(id + "/format-color", fields.Localize("文字颜色"), ref m_color);
        fields.Text(id + "/format-font", fields.Localize("页面字体资源键（可留空）"), ref m_font);
        fields.Number(id + "/format-outline", fields.Localize("描边半径"), ref m_outline, 0, 8);
        fields.Color(id + "/format-outline-color", fields.Localize("描边颜色"), ref m_outlineColor);
        fields.Number(id + "/format-shadow", fields.Localize("阴影偏移"), ref m_shadow, -16, 16);
        fields.Color(id + "/format-shadow-color", fields.Localize("阴影颜色"), ref m_shadowColor);
        bool changed = false;
        try
        {
            if (document.elements.Count > 0 && ImGui.Button(fields.Caption("应用格式到所选范围##" + id, fields.Localize("应用格式到所选范围##") + id)))
            {
                fields.Finish(); source = TextFormattingEditService.ApplyStyle(source, (int)m_first, (int)m_count,
                    new(m_bold, m_italic, (float)m_size, m_colorEnabled ? m_color : null, m_font, (float)m_outline, m_outlineColor, (float)m_shadow, m_shadowColor)); changed = true;
            }
            fields.Text(id + "/format-ruby", fields.Localize("注音（留空可移除）"), ref m_ruby);
            if (document.elements.Count > 0 && ImGui.Button(fields.Caption("应用注音到所选范围##" + id, fields.Localize("应用注音到所选范围##") + id)))
            { fields.Finish(); source = TextFormattingEditService.ApplyRuby(source, (int)m_first, (int)m_count, m_ruby); changed = true; }
            fields.Number(id + "/format-pause", fields.Localize("句末停顿秒数"), ref m_pause, 0, 60);
            if (ImGui.Button(fields.Caption("追加停顿##" + id, fields.Localize("追加停顿##") + id))) { fields.Finish(); source += "[pause=" + m_pause.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "]"; changed = true; }
            fields.Number(id + "/format-speed", fields.Localize("所选文字速度倍率"), ref m_speed, 0.01, 100);
            if (document.elements.Count > 0 && ImGui.Button(fields.Caption("应用局部速度##" + id, fields.Localize("应用局部速度##") + id)))
            { fields.Finish(); source = TextFormattingEditService.ApplySpeed(source, (int)m_first, (int)m_count, m_speed); changed = true; }
            fields.Text(id + "/format-icon", fields.Localize("页面图片资源键"), ref m_icon);
            if (m_icon.Length > 0 && ImGui.Button(fields.Caption("追加行内图片##" + id, fields.Localize("追加行内图片##") + id)))
            { string candidate = source + "[icon=" + m_icon + "]"; _ = NovelTextMarkup.Parse(candidate); fields.Finish(); source = candidate; changed = true; }
            if (changed) m_error = string.Empty;
        }
        catch (Exception failure) when (failure is FormatException or InvalidOperationException or ArgumentOutOfRangeException) { m_error = fields.Error(failure); }
        if (m_error.Length > 0) EditorUiSession.current.widgets.Hint(m_error);
        return changed;
    }
}

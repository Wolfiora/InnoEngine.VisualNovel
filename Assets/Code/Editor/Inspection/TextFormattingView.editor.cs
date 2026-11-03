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
        if (!ImGui.CollapsingHeader("Text formatting and pacing##" + id)) return false;
        NovelTextDocument document;
        try { document = NovelTextMarkup.Parse(source); }
        catch (FormatException failure) { EditorUiSession.current.widgets.Hint((failure).Message); return false; }
        EditorUiSession.current.widgets.Hint("Text preview · " + document.text);
        fields.Number(id + "/format-first", "Starting grapheme (zero-based)", ref m_first, 0, Math.Max(0, document.elements.Count - 1));
        fields.Number(id + "/format-count", "Selected grapheme count", ref m_count, 1, Math.Max(1, document.elements.Count - (int)m_first));
        fields.Toggle(id + "/bold", "Bold", ref m_bold); fields.Toggle(id + "/italic", "Italic", ref m_italic);
        fields.Number(id + "/format-size", "Font size (0 inherits the screen)", ref m_size, 0, 512);
        fields.Toggle(id + "/format-color-enabled", "Override text color", ref m_colorEnabled);
        if (m_colorEnabled) fields.Color(id + "/format-color", "Text color", ref m_color);
        fields.Text(id + "/format-font", "Screen font resource key (optional)", ref m_font);
        fields.Number(id + "/format-outline", "Outline radius", ref m_outline, 0, 8);
        fields.Color(id + "/format-outline-color", "Outline color", ref m_outlineColor);
        fields.Number(id + "/format-shadow", "Shadow offset", ref m_shadow, -16, 16);
        fields.Color(id + "/format-shadow-color", "Shadow color", ref m_shadowColor);
        bool changed = false;
        try
        {
            if (document.elements.Count > 0 && ImGui.Button("Apply formatting to selected range##" + id))
            {
                fields.Finish(); source = TextFormattingEditService.ApplyStyle(source, (int)m_first, (int)m_count,
                    new(m_bold, m_italic, (float)m_size, m_colorEnabled ? m_color : null, m_font, (float)m_outline, m_outlineColor, (float)m_shadow, m_shadowColor)); changed = true;
            }
            fields.Text(id + "/format-ruby", "Ruby annotation (empty to remove)", ref m_ruby);
            if (document.elements.Count > 0 && ImGui.Button("Apply ruby annotation to selected range##" + id))
            { fields.Finish(); source = TextFormattingEditService.ApplyRuby(source, (int)m_first, (int)m_count, m_ruby); changed = true; }
            fields.Number(id + "/format-pause", "End-of-line pause (seconds)", ref m_pause, 0, 60);
            if (ImGui.Button("Append pause##" + id)) { fields.Finish(); source += "[pause=" + m_pause.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "]"; changed = true; }
            fields.Number(id + "/format-speed", "Selected text speed multiplier", ref m_speed, 0.01, 100);
            if (document.elements.Count > 0 && ImGui.Button("Apply local speed##" + id))
            { fields.Finish(); source = TextFormattingEditService.ApplySpeed(source, (int)m_first, (int)m_count, m_speed); changed = true; }
            fields.Text(id + "/format-icon", "Screen image resource key", ref m_icon);
            if (m_icon.Length > 0 && ImGui.Button("Append inline image##" + id))
            { string candidate = source + "[icon=" + m_icon + "]"; _ = NovelTextMarkup.Parse(candidate); fields.Finish(); source = candidate; changed = true; }
            if (changed) m_error = string.Empty;
        }
        catch (Exception failure) when (failure is FormatException or InvalidOperationException or ArgumentOutOfRangeException) { m_error = (failure).Message; }
        if (m_error.Length > 0) EditorUiSession.current.widgets.Hint(m_error);
        return changed;
    }
}

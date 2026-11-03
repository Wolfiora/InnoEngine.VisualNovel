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
        if (!ImGui.CollapsingHeader("Screen rich-text resources")) return false;
        EditorUiSession.current.widgets.Hint("Assign names to fonts and inline images. Text uses these resource keys to reference imported assets; packaging follows their native dependencies.");
        bool changed = false;
        foreach (UiTextFont item in screen.textFonts)
        {
            string key = item.key;
            if (fields.Text("text-font/" + item.key, "Font resource key", ref key))
            {
                if (Valid(key) && screen.textFonts.All(other => ReferenceEquals(other, item) || other.key != key)) { item.key = key; changed = true; m_error = string.Empty; }
                else m_error = "Resource keys must be unique and contain only letters, digits, _, -, . or /.";
            }
            ImGui.Text(item.font is null ? "Missing · No font bound" : "Bound font · " + item.font.identity.persistentId);
            if (NovelResourceSelection.Get<FontAsset>(interactions) is { } font && ImGui.Button("Bind selected font##" + item.key)) { fields.Finish(); item.font = font; changed = true; }
            if (ImGui.Button("Remove this font binding##" + item.key)) { fields.Finish(); screen.textFonts = screen.textFonts.Where(other => !ReferenceEquals(item, other)).ToArray(); changed = true; break; }
        }
        if (ImGui.Button("Add font binding"))
        { fields.Finish(); screen.textFonts = [.. screen.textFonts, new() { key = "font-" + Guid.NewGuid().ToString("N")[..8], font = NovelResourceSelection.Get<FontAsset>(interactions) }]; changed = true; }
        foreach (UiTextIcon item in screen.textIcons)
        {
            string key = item.key;
            if (fields.Text("text-icon/" + item.key, "Inline image resource key", ref key))
            {
                if (Valid(key) && screen.textIcons.All(other => ReferenceEquals(other, item) || other.key != key)) { item.key = key; changed = true; m_error = string.Empty; }
                else m_error = "Resource keys must be unique and contain only letters, digits, _, -, . or /.";
            }
            ImGui.Text(item.image is null ? "Missing · No image bound" : "Bound image · " + item.image.identity.persistentId);
            if (NovelResourceSelection.Get<TextureAsset>(interactions) is { } image && ImGui.Button("Bind selected inline image##" + item.key)) { fields.Finish(); item.image = image; changed = true; }
            if (ImGui.Button("Remove this image binding##" + item.key)) { fields.Finish(); screen.textIcons = screen.textIcons.Where(other => !ReferenceEquals(item, other)).ToArray(); changed = true; break; }
        }
        if (ImGui.Button("Add inline image binding"))
        { fields.Finish(); screen.textIcons = [.. screen.textIcons, new() { key = "icon-" + Guid.NewGuid().ToString("N")[..8], image = NovelResourceSelection.Get<TextureAsset>(interactions) }]; changed = true; }
        if (m_error.Length > 0) EditorUiSession.current.widgets.Hint(m_error);
        return changed;
    }
    private static bool Valid(string key) => Inno.VisualNovel.Model.Text.NovelTextMarkup.IsResourceKey(key);
}

using System.Linq;
using InnoEngine.Text;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.UI.Input;

namespace Inno.VisualNovel.Editor.ScreenDesigner;

internal sealed class ScreenSettingsView(StoryInspectorFields fields)
{
    internal bool Draw(
        ScreenAsset screen,
        EditorInteractions interactions
    ) {
        if (!ImGui.CollapsingHeader("Screen settings")) return false;
        string title = screen.title, focus = screen.initialFocus; double width = screen.width, height = screen.height; bool modal = screen.modal;
        bool changed = fields.Text("page/title", "Screen name", ref title); screen.title = title;
        changed |= fields.Number("page/width", "Design width", ref width, 1, 16384); screen.width = (float)width;
        changed |= fields.Number("page/height", "Design height", ref height, 1, 16384); screen.height = (float)height;
        changed |= fields.Toggle("page/modal", "Consume input outside controls", ref modal); screen.modal = modal;
        changed |= fields.Select("page/focus", "Initial keyboard focus", ref focus, new[] { ("", "Automatically choose an interactive control") }
            .Concat(UiFocusTargets.Enumerate(screen.root).Select(static item => (item.id, item.kind + " · " + item.id)))); screen.initialFocus = focus;
        if (NovelResourceSelection.Get<FontAsset>(interactions) is { } font && ImGui.Button("Use selected font as screen default"))
        { fields.Finish(); screen.font = font; changed = true; }
        if (screen.font is not null) ImGui.Text("Default font · " + screen.font.name);
        return changed;
    }
}

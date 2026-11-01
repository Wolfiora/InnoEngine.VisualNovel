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
        if (!ImGui.CollapsingHeader(fields.Label("页面设置"))) return false;
        string title = screen.title, focus = screen.initialFocus; double width = screen.width, height = screen.height; bool modal = screen.modal;
        bool changed = fields.Text("page/title", fields.Localize("页面名称"), ref title); screen.title = title;
        changed |= fields.Number("page/width", fields.Localize("设计宽度"), ref width, 1, 16384); screen.width = (float)width;
        changed |= fields.Number("page/height", fields.Localize("设计高度"), ref height, 1, 16384); screen.height = (float)height;
        changed |= fields.Toggle("page/modal", "消费未命中控件的输入", ref modal); screen.modal = modal;
        changed |= fields.Select("page/focus", fields.Localize("初始键盘焦点"), ref focus, new[] { ("", fields.Localize("自动选择可交互控件")) }
            .Concat(UiFocusTargets.Enumerate(screen.root).Select(static item => (item.id, item.kind + " · " + item.id)))); screen.initialFocus = focus;
        if (NovelResourceSelection.Get<FontAsset>(interactions) is { } font && ImGui.Button(fields.Label("使用所选字体作为页面默认字体")))
        { fields.Finish(); screen.font = font; changed = true; }
        if (screen.font is not null) ImGui.Text(fields.Localize("默认字体 · ") + screen.font.name);
        return changed;
    }
}

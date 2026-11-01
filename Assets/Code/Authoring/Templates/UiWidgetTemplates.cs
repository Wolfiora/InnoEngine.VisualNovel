using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Text;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Authoring.Templates;

/// <summary>Provides reusable editable controls built from the same native game shell declarations.</summary>
public static class UiWidgetTemplates
{
    /// <summary>Gets stable preset identities, titles and intended game contexts.</summary>
    public static IReadOnlyList<(string id, string title, string description)> entries { get; } = Array.AsReadOnly(new (string, string, string)[]
    {
        ("button", "阅读按钮", "真实 advance 动作；可在检查器改为其他系统动作。"),
        ("dialogue", "ADV 对话组件", "姓名、头像、正文与滚动；用于阅读页。"),
        ("choices", "剧情选项组件", "真实 choices 列表、启用条件和稳定选项动作。"),
        ("toolbar", "阅读工具栏", "菜单、历史、自动、快进、隐藏和回退动作。"),
        ("text-speed", "文字速度设置行", "标签与类型化滑块；绑定玩家文字速度。"),
        ("confirmation", "确认页面组件", "确认/取消按钮和动态提示；用于实际确认页上下文。"),
        ("disclosure", "可折叠状态卡片", "本次页面展开开关与随游戏保存的标记；实例具有独立字段，不需要脚本。")
    });
    /// <summary>Creates a detached component whose bindings and actions remain ordinary editable UI data.</summary>
    /// <param name="id">One declared preset identity.</param>
    /// <param name="font">The borrowed native page font.</param>
    /// <returns>A reusable native screen asset ready to save or insert.</returns>
    public static ScreenAsset Create(
        string id,
        FontAsset font
    ) {
        if (id == "disclosure") return new() { title = "可折叠状态卡片", width = 600, height = 240, font = font,
            stateVariables = [new() { id = "expanded", name = "显示详情", scope = VariableScope.Local, initialValue = NovelValue.Boolean(false) },
                new() { id = "marked", name = "保留的标记", scope = VariableScope.Game, initialValue = NovelValue.Boolean(false) }],
            root = new() { id = "card", kind = UiElementKind.Vertical, layout = new() { width = 600, height = 240, padding = 16, gap = 12 },
                style = new() { background = 0x233149FF }, children = [
                    new() { id = "expand", kind = UiElementKind.Button, text = "展开 / 收起详情", action = "page.toggle", argument = "expanded",
                        layout = new() { widthMode = UiSizeMode.Fill, height = 60 } },
                    new() { id = "details", kind = UiElementKind.Vertical, visibleBinding = "page.expanded", layout = new() { widthMode = UiSizeMode.Fill, height = 130, gap = 8 },
                        children = [new() { id = "hint", kind = UiElementKind.Text, text = "展开状态离开页面即重置；下方标记随游戏保存。", layout = new() { widthMode = UiSizeMode.Fill, height = 56 } },
                            new() { id = "mark", kind = UiElementKind.Toggle, text = "保留这项标记", binding = "page.marked", layout = new() { widthMode = UiSizeMode.Fill, height = 56 } }] }] } };
        if (id == "button") return new() { title = "阅读按钮", width = 320, height = 80, font = font, root = new()
        { id = "button", kind = UiElementKind.Button, text = "继续阅读", action = "advance", layout = new() { width = 320, height = 80 },
            style = new() { background = 0x2B3D56FF, hover = 0x3B5671FF, pressed = 0x4C6D8CFF, disabled = 0x252F3EFF, foreground = 0xF2F5FCFF, focus = 0xA5D6F1FF, fontSize = 28 } } };
        if (!entries.Any(entry => entry.id == id)) throw new ArgumentException("Unknown native widget preset: " + id, nameof(id));
        var entry = entries.Single(entry => entry.id == id);
        string role = id == "text-speed" ? "settings" : id == "confirmation" ? "confirm" : "reading";
        var source = ShellScreenTemplates.Create(role, font, "Clear");
        return UiWidgetEditService.Extract(source, id == "confirmation" ? "root" : id, entry.title);
    }
}

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
        ("button", "Reading button", "Invokes the advance action; choose another system action in Inspector."),
        ("dialogue", "ADV dialogue", "Speaker name, portrait, dialogue and scrolling for the reading page."),
        ("choices", "Story choices", "Binds the choices collection, availability conditions and stable choice actions."),
        ("toolbar", "Reading toolbar", "Menu, history, auto, skip, hide and rollback actions."),
        ("text-speed", "Text speed setting", "Label and typed slider bound to the player text speed."),
        ("confirmation", "Confirmation", "Confirm and cancel actions with a bound message for confirmation pages."),
        ("disclosure", "Collapsible state card", "Page-local expansion and a saved game flag; each instance owns its fields without scripting.")
    });
    /// <summary>Creates a detached component whose bindings and actions remain ordinary editable UI data.</summary>
    /// <param name="id">One declared preset identity.</param>
    /// <param name="font">The borrowed native page font.</param>
    /// <returns>A reusable native screen asset ready to save or insert.</returns>
    public static ScreenAsset Create(
        string id,
        FontAsset font
    ) {
        if (id == "disclosure") return new() { title = "Collapsible state card", width = 600, height = 240, font = font,
            stateVariables = [new() { id = "expanded", name = "显示详情", scope = VariableScope.Local, initialValue = NovelValue.Boolean(false) },
                new() { id = "marked", name = "保留的标记", scope = VariableScope.Game, initialValue = NovelValue.Boolean(false) }],
            root = new() { id = "card", kind = UiElementKind.Vertical, layout = new() { width = 600, height = 240, padding = 16, gap = 12 },
                style = new() { background = 0x233149FF }, children = [
                    new() { id = "expand", kind = UiElementKind.Button, text = "展开 / 收起详情", action = "page.toggle", argument = "expanded",
                        layout = new() { widthMode = UiSizeMode.Fill, height = 60 } },
                    new() { id = "details", kind = UiElementKind.Vertical, visibleBinding = "page.expanded", layout = new() { widthMode = UiSizeMode.Fill, height = 130, gap = 8 },
                        children = [new() { id = "hint", kind = UiElementKind.Text, text = "展开状态离开页面即重置；下方标记随游戏保存。", layout = new() { widthMode = UiSizeMode.Fill, height = 56 } },
                            new() { id = "mark", kind = UiElementKind.Toggle, text = "保留这项标记", binding = "page.marked", layout = new() { widthMode = UiSizeMode.Fill, height = 56 } }] }] } };
        if (id == "button") return new() { title = "Reading button", width = 320, height = 80, font = font, root = new()
        { id = "button", kind = UiElementKind.Button, text = "继续阅读", action = "advance", layout = new() { width = 320, height = 80 },
            style = new() { background = 0x2B3D56FF, hover = 0x3B5671FF, pressed = 0x4C6D8CFF, disabled = 0x252F3EFF, foreground = 0xF2F5FCFF, focus = 0xA5D6F1FF, fontSize = 28 } } };
        if (!entries.Any(entry => entry.id == id)) throw new ArgumentException("Unknown native widget preset: " + id, nameof(id));
        var entry = entries.Single(entry => entry.id == id);
        string role = id == "text-speed" ? "settings" : id == "confirmation" ? "confirm" : "reading";
        var source = ShellScreenTemplates.Create(role, font, "Clear");
        return UiWidgetEditService.Extract(source, id == "confirmation" ? "root" : id, entry.title);
    }
}

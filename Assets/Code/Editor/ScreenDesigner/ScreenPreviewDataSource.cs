using System.Collections.Generic;
using System;
using System.Linq;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.UI.Binding;

namespace Inno.VisualNovel.Editor.ScreenDesigner;

internal sealed class ScreenPreviewDataSource : UiDataSource
{
    private readonly Player.Shell.PlayerPreferences m_preferences = new();
    private readonly Player.Binding.KeyboardSettingsDataSource m_keyboard;
    internal ScreenPreviewDataSource() { m_keyboard = new(() => m_preferences, static (_, text) => text); }
    internal void SetPage(VariableDefinition[] fields)
    {
        foreach (string key in m_values.Keys.Where(static key => key.StartsWith("page.", StringComparison.Ordinal)).ToArray()) m_values.Remove(key);
        foreach (var field in fields) m_values["page." + field.id] = field.initialValue.Copy();
    }
    private readonly Dictionary<string, NovelValue> m_values = new()
    {
        ["game.title"] = NovelValue.String("页面设计预览"), ["game.active"] = NovelValue.Boolean(true),
        ["stage.empty"] = NovelValue.Boolean(true), ["dialogue.speaker"] = NovelValue.String("角色姓名"),
        ["dialogue.text"] = NovelValue.String("这是使用正式游戏 UI 与文字渲染器的预览。\n切换布局与字号，检查长台词和控件位置。"),
        ["dialogue.visible"] = NovelValue.Boolean(true), ["dialogue.adv"] = NovelValue.Boolean(true), ["reading.toolbarVisible"] = NovelValue.Boolean(true), ["choice.visible"] = NovelValue.Boolean(true),
        ["game.theme"] = NovelValue.String("builtin.theme.clear"),
        ["choice.timed"] = NovelValue.Boolean(true), ["choice.timeRemaining"] = NovelValue.Number(6.5),
        ["choice.timeRemainingRatio"] = NovelValue.Number(.65), ["choice.remainingSeconds"] = NovelValue.Integer(7),
        ["reading.advanceVisible"] = NovelValue.Boolean(true), ["reading.waitingForAdvance"] = NovelValue.Boolean(false),
        ["settings.textSpeed"] = NovelValue.Number(1), ["settings.autoDelay"] = NovelValue.Number(1.5),
        ["settings.fontScale"] = NovelValue.Number(1), ["settings.masterVolume"] = NovelValue.Number(0.8),
        ["settings.reducedMotion"] = NovelValue.Boolean(false), ["settings.skipReadOnly"] = NovelValue.Boolean(true), ["settings.language"] = NovelValue.String("zh-CN")
    };
    public override bool TryRead(string path, out NovelValue value)
    {
        if (m_keyboard.TryRead(path, out value)) return true;
        if (m_values.TryGetValue(path, out NovelValue? found)) { value = found.Copy(); return true; }
        value = NovelValue.String(string.Empty); return false;
    }
    public override bool TryWrite(
        string path,
        NovelValue value
    ) {
        if (path == "settings.holdSkipModifiers") return m_keyboard.TryWrite(path, value);
        if (path.StartsWith("item.", StringComparison.Ordinal)) return false;
        m_values[path] = value.Copy(); return true;
    }
    public override bool TryWrite(
        string path,
        NovelValue value,
        UiDataRow? row
    ) => path == "item.shortcutKey" ? m_keyboard.TryWrite(path, value, row) : base.TryWrite(path, value, row);
    public override IReadOnlyList<UiDataRow> ReadRows(string path) => path is "settings.shortcuts" or "settings.shortcutKeys" or "settings.holdSkipChoices" or "settings.navigationShortcuts" or "settings.navigationKeys"
        ? m_keyboard.ReadRows(path) : path == "languages" ? [
        new("zh-CN", new Dictionary<string, NovelValue> { ["text"] = NovelValue.String("简体中文"), ["value"] = NovelValue.String("zh-CN") }),
        new("en", new Dictionary<string, NovelValue> { ["text"] = NovelValue.String("English"), ["value"] = NovelValue.String("en") })] : path == "choices" ? [
        new("one", new Dictionary<string, NovelValue> { ["text"] = NovelValue.String("第一个示例选项"), ["id"] = NovelValue.String("one"), ["enabled"] = NovelValue.Boolean(true) }),
        new("two", new Dictionary<string, NovelValue> { ["text"] = NovelValue.String("用于检查文本扩张和布局的较长选项"), ["id"] = NovelValue.String("two"), ["enabled"] = NovelValue.Boolean(true) })] : [];
}

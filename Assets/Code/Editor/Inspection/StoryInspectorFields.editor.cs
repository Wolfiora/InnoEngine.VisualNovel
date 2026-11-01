using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Editor.Localization;

namespace Inno.VisualNovel.Editor.Inspection;

internal sealed class StoryInspectorFields(EditorInteractions interactions)
{
    private readonly Dictionary<string, string> m_pending = new(StringComparer.Ordinal);
    private readonly HashSet<string> m_visible = new(StringComparer.Ordinal);
    private EditorEditGesture? m_gesture;
    private string m_field = string.Empty;
    private bool m_active;
    private string m_textFocus = string.Empty;
    private string m_textActive = string.Empty, m_nextTextActive = string.Empty;
    private readonly Dictionary<string, int> m_paragraphs = new(StringComparer.Ordinal);
    private readonly HashSet<string> m_multilineVisible = new(StringComparer.Ordinal);
    private readonly Dictionary<string, InspectorChoiceMenu> m_choices = new(StringComparer.Ordinal);
    private readonly HashSet<string> m_choiceVisible = new(StringComparer.Ordinal);
    internal EditorTextView text { get; } = new(interactions);

    internal string? mergeKey => m_gesture?.mergeKey;
    internal string Localize(string source) => text.Localize(source);
    internal string Error(Exception error) => text.Error(error);
    internal IEnumerable<(string id, string name)> BuiltIn(IEnumerable<(string id, string name)> choices) => text.BuiltIn(choices);
    internal string Label(string source) => text.Label(source);
    internal string Caption(
        string source,
        string translated
    ) => text.Caption(source, translated);
    internal string Format(
        string source,
        params object?[] arguments
    ) => text.Format(source, arguments);
    internal bool Color(
        string id,
        string label,
        ref uint value
    ) {
        bool changed = NovelColorField.Draw(text, id, label, ref value);
        Observe(id, changed); return changed;
    }
    internal void BeginFrame() { m_active = false; m_nextTextActive = string.Empty; m_visible.Clear(); m_multilineVisible.Clear(); m_choiceVisible.Clear(); }

    internal void EndFrame()
    {
        if (m_gesture is not null && ImGui.IsKeyPressed(ImGuiKey.Escape, false))
        {
            EditorHistoryResult result = m_gesture.Cancel();
            if (!result.succeeded) throw new InvalidOperationException(result.message);
            m_gesture = null;
            m_pending.Clear();
        }
        else if (!m_active) Finish();
        foreach (string key in m_pending.Keys.Where(key => !m_visible.Contains(key)).ToArray()) m_pending.Remove(key);
        foreach (string key in m_paragraphs.Keys.Where(key => !m_multilineVisible.Contains(key)).ToArray()) m_paragraphs.Remove(key);
        foreach (string key in m_choices.Keys.Where(key => !m_choiceVisible.Contains(key)).ToArray()) m_choices.Remove(key);
        m_textActive = m_nextTextActive;
    }

    internal void Finish()
    {
        m_gesture?.Commit();
        m_gesture = null;
        m_field = string.Empty;
        m_pending.Clear();
        m_textActive = m_nextTextActive = string.Empty;
    }

    internal bool Text(
        string id,
        string label,
        ref string value
    ) => Text(id, label, ref value, false, out _);
    internal void FocusText(string id) => m_textFocus = id;
    private bool Text(
        string id,
        string label,
        ref string value,
        bool reportSubmit,
        out bool submitted
    ) {
        string candidate = value;
        string original = value; bool changed = false, accepted = false;
        EditorUiSession.current.widgets.PropertyRow(id, Localize(label), () =>
        {
            if (m_textFocus == id) { ImGui.SetKeyboardFocusHere(); m_textFocus = string.Empty; }
            ImGui.InputText("##" + id, ref candidate, 32768);
            bool active = ImGui.IsItemActive();
            accepted = reportSubmit && ImGui.IsWindowFocused() && ImGui.IsKeyPressed(ImGuiKey.Enter, false)
                && (active || m_textActive == id && !ImGui.IsAnyItemActive());
            changed = candidate != original;
            Observe(id, changed);
            if (active) m_nextTextActive = id;
        });
        value = candidate; submitted = accepted;
        return changed;
    }

    internal bool MultilineText(
        string id,
        string label,
        ref string value
    ) => MultilineText(id, label, ref value, false, out _);
    internal bool WritingText(
        string id,
        string label,
        ref string value,
        out bool continueDialogue
    ) => MultilineText(id, label, ref value, true, out continueDialogue);
    private bool MultilineText(
        string id,
        string label,
        ref string value,
        bool writing,
        out bool continueDialogue
    ) {
        continueDialogue = false;
        label = Localize(label);
        m_multilineVisible.Add(id);
        string[] lines = Inno.VisualNovel.Authoring.Documents.ParagraphText.Lines(value);
        int active = Math.Clamp(m_paragraphs.GetValueOrDefault(id), 0, lines.Length - 1);
        bool changed = false, insert = false;
        for (int index = 0; index < lines.Length; index++)
        {
            string lineId = id + "/paragraph/" + index;
            changed |= Text(lineId, lines.Length == 1 ? label : label + " · " + (index + 1), ref lines[index], true, out bool submitted);
            if (!ImGui.IsItemActive() && !submitted) continue;
            active = index;
            if (submitted)
            {
                continueDialogue = writing && (lines.Length == 1 && lines[0].Length == 0 || ImGui.IsKeyDown(ImGuiKey.LeftCtrl) || ImGui.IsKeyDown(ImGuiKey.RightCtrl)
                    || ImGui.IsKeyDown(ImGuiKey.LeftSuper) || ImGui.IsKeyDown(ImGuiKey.RightSuper));
                if (!continueDialogue) insert = true;
            }
        }
        m_paragraphs[id] = active;
        string candidate = changed ? string.Join('\n', lines) : value;
        if (ImGui.Button(text.Caption(Label("在当前行后换段##" + id), Label(text.Localize("在当前行后换段##") + id)))) insert = true;
        if (insert)
        {
            Finish(); candidate = Inno.VisualNovel.Authoring.Documents.ParagraphText.InsertAfter(candidate, active);
            m_paragraphs[id] = active + 1; m_textFocus = id + "/paragraph/" + (active + 1); changed = true;
        }
        if (!insert && lines.Length > 1 && lines[active].Length == 0)
        {
            ImGui.SameLine();
            if (ImGui.Button(text.Caption(Label("删除当前空行##" + id), Label(text.Localize("删除当前空行##") + id))))
            {
                Finish(); candidate = Inno.VisualNovel.Authoring.Documents.ParagraphText.RemoveEmpty(candidate, active);
                m_paragraphs[id] = Math.Max(0, active - 1); changed = true;
            }
        }
        if (changed) value = candidate;
        return changed;
    }

    internal bool Number(
        string id,
        string label,
        ref double value,
        double minimum = -double.MaxValue,
        double maximum = double.MaxValue
    ) {
        string candidate = m_pending.GetValueOrDefault(id) ?? value.ToString("R", CultureInfo.InvariantCulture);
        bool edited = Text(id, label, ref candidate);
        m_pending[id] = candidate;
        m_visible.Add(id);
        bool valid = double.TryParse(candidate, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            && double.IsFinite(parsed) && parsed >= minimum && parsed <= maximum;
        if (!valid) EditorUiSession.current.widgets.Hint(Format("{0}：请输入 {1:G} 至 {2:G} 的有限数值。", Localize(label), minimum, maximum));
        if (!edited || !valid) return false;
        value = parsed;
        return true;
    }

    internal bool Integer(
        string id,
        string label,
        ref long value
    ) {
        string candidate = m_pending.GetValueOrDefault(id) ?? value.ToString(CultureInfo.InvariantCulture);
        bool edited = Text(id, label, ref candidate);
        m_pending[id] = candidate;
        m_visible.Add(id);
        bool valid = long.TryParse(candidate, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed);
        if (!valid) EditorUiSession.current.widgets.Hint(label + text.Localize("：请输入有效的 64 位整数。"));
        if (!edited || !valid) return false;
        value = parsed;
        return true;
    }

    internal bool Toggle(
        string id,
        string label,
        ref bool value
    ) {
        bool candidate = value;
        bool changed = false;
        EditorUiSession.current.widgets.PropertyRow(id, Localize(label), () => changed = ImGui.Checkbox("##" + id, ref candidate));
        if (changed) Finish();
        value = candidate;
        return changed;
    }

    internal bool Select(
        string id,
        string label,
        ref string value,
        IEnumerable<(string id, string name)> options
    ) {
        string candidate = value;
        bool changed = false;
        var items = options.ToArray();
        m_choiceVisible.Add(id);
        string preview = items.FirstOrDefault(item => item.id == candidate).name ?? (candidate.Length == 0 ? Localize("未分配") : "Missing · " + candidate);
        var widgets = EditorUiSession.current.widgets;
        widgets.PropertyRow(id, Localize(label), () =>
        {
            if (!widgets.BeginBoundedCombo("##" + id, preview)) return;
            try
            {
                if (!m_choices.TryGetValue(id, out var menu)) m_choices.Add(id, menu = new());
                if (items.Length > 16)
                {
                    ImGui.Text(Localize(text.Localize("搜索名称或标识")));
                    string query = menu.search;
                    if (ImGui.InputText("##choice-filter/" + id, ref query, 512)) { menu.search = query; menu.page = 0; }
                    if (query.Length > 0 && ImGui.SmallButton(text.Caption(Label("清除筛选##choice/" + id), Label(text.Localize("清除筛选##choice/") + id)))) { menu.search = string.Empty; menu.page = 0; }
                }
                else { menu.search = string.Empty; menu.page = 0; }
                foreach (var item in menu.Read(items))
                    if (ImGui.Selectable(item.name + "###vn-choice/" + item.id, item.id == candidate))
                    {
                        Finish(); candidate = item.id; changed = true;
                    }
                if (menu.matches == 0) widgets.Hint(Localize(text.Localize("没有匹配的选项。当前值保持不变。")));
                if (menu.pages > 1)
                {
                    ImGui.Text(Format("第 {0}/{1} 页", menu.page + 1, menu.pages));
                    if (menu.page > 0 && ImGui.SmallButton(text.Caption(Label("上一页##choice/" + id), Label(text.Localize("上一页##choice/") + id)))) menu.page--;
                    if (menu.page + 1 < menu.pages && ImGui.SmallButton(text.Caption(Label("下一页##choice/" + id), Label(text.Localize("下一页##choice/") + id)))) menu.page++;
                }
            }
            finally { widgets.EndBoundedCombo(); }
        });
        value = candidate;
        return changed;
    }

    internal bool SelectEnum<T>(
        string id,
        string label,
        ref T value
    ) where T : struct, Enum
    {
        string candidate = value.ToString();
        if (!Select(id, label, ref candidate, Enum.GetValues<T>().Select(static item => (item.ToString(), item.ToString())))) return false;
        value = Enum.Parse<T>(candidate);
        return true;
    }

    private void Observe(
        string id,
        bool changed
    ) {
        m_active |= ImGui.IsItemActive();
        if (!changed) return;
        if (m_field != id) Finish();
        m_field = id;
        m_gesture ??= new(interactions.history, Localize("编辑剧情参数"));
    }
}

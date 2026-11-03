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

    internal string? mergeKey => m_gesture?.mergeKey;
    internal bool Color(
        string id,
        string label,
        ref uint value
    ) {
        bool changed = NovelColorField.Draw( id, label, ref value);
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
        EditorUiSession.current.widgets.PropertyRow(id, label, () =>
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
        label = label;
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
        if (ImGui.Button("Insert paragraph after current line##" + id)) insert = true;
        if (insert)
        {
            Finish(); candidate = Inno.VisualNovel.Authoring.Documents.ParagraphText.InsertAfter(candidate, active);
            m_paragraphs[id] = active + 1; m_textFocus = id + "/paragraph/" + (active + 1); changed = true;
        }
        if (!insert && lines.Length > 1 && lines[active].Length == 0)
        {
            ImGui.SameLine();
            if (ImGui.Button("Remove current empty line##" + id))
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
        if (!valid) EditorUiSession.current.widgets.Hint(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0}: enter a finite number from {1:G} to {2:G}.", label, minimum, maximum));
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
        if (!valid) EditorUiSession.current.widgets.Hint(label + ": enter a valid 64-bit integer.");
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
        EditorUiSession.current.widgets.PropertyRow(id, label, () => changed = ImGui.Checkbox("##" + id, ref candidate));
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
        string preview = items.FirstOrDefault(item => item.id == candidate).name ?? (candidate.Length == 0 ? "Unassigned" : "Missing · " + candidate);
        var widgets = EditorUiSession.current.widgets;
        widgets.PropertyRow(id, label, () =>
        {
            if (!widgets.BeginBoundedCombo("##" + id, preview)) return;
            try
            {
                if (!m_choices.TryGetValue(id, out var menu)) m_choices.Add(id, menu = new());
                if (items.Length > 16)
                {
                    ImGui.Text("Search name or ID");
                    string query = menu.search;
                    if (ImGui.InputText("##choice-filter/" + id, ref query, 512)) { menu.search = query; menu.page = 0; }
                    if (query.Length > 0 && ImGui.SmallButton("Clear filter##choice/" + id)) { menu.search = string.Empty; menu.page = 0; }
                }
                else { menu.search = string.Empty; menu.page = 0; }
                foreach (var item in menu.Read(items))
                    if (ImGui.Selectable(item.name + "###vn-choice/" + item.id, item.id == candidate))
                    {
                        Finish(); candidate = item.id; changed = true;
                    }
                if (menu.matches == 0) widgets.Hint("No matching options. The current value is retained.");
                if (menu.pages > 1)
                {
                    ImGui.Text(string.Format(System.Globalization.CultureInfo.InvariantCulture,"Page {0} of {1}", menu.page + 1, menu.pages));
                    if (menu.page > 0 && ImGui.SmallButton("Previous page##choice/" + id)) menu.page--;
                    if (menu.page + 1 < menu.pages && ImGui.SmallButton("Next page##choice/" + id)) menu.page++;
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
        m_gesture ??= new(interactions.history, "Edit story parameter");
    }
}

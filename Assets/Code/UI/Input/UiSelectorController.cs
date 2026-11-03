using InnoEngine.Mathematics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.UI.Binding;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.UI.Input;

internal sealed class UiSelectorController
{
    private sealed record Option(string id, string text, NovelValue value, bool enabled);
    private UiElementInstance? m_owner;
    private int m_selected, m_first, m_visible;
    private string m_pressed = string.Empty;
    private string m_selectedId = string.Empty;
    private string m_query = string.Empty;
    private Rect m_bounds;
    private Rect m_rows;
    internal bool active => m_owner is not null;
    internal string query => m_query;
    internal string error { get; private set; } = string.Empty;
    private static Option[] Options(
        UiElementDefinition definition,
        UiDataSource source
    ) {
        Option[] options = definition.optionsBinding.Length == 0
            ? definition.options.Select(option => new Option(option.id, source.Localize(option.textId, option.text), option.value.Copy(), option.enabled)).ToArray()
            : source.ReadRows(definition.optionsBinding).Select(row => new Option(row.key,
                row.values.TryGetValue("text", out NovelValue? text) ? text.ToString() : row.key,
                row.values.TryGetValue("value", out NovelValue? value) ? value.Copy() : throw new InvalidOperationException("Selector row is missing a typed value: " + definition.id + "/" + row.key),
                !row.values.TryGetValue("enabled", out NovelValue? enabled) || enabled.kind == NovelValueKind.Boolean && enabled.boolean)).ToArray();
        if (options.Length > 1000 || options.Any(static option => string.IsNullOrWhiteSpace(option.id)) || options.Select(static option => option.id).Distinct(StringComparer.Ordinal).Count() != options.Length)
            throw new InvalidOperationException("Selectors require up to 1000 uniquely identified options: " + definition.id);
        return options;
    }
    internal static string Label(
        UiElementDefinition definition,
        UiDataSource source,
        NovelValue? value
    ) => (value is null ? source.Localize(definition.textId, definition.text) : Options(definition, source).FirstOrDefault(option => option.value.Equals(value))?.text ?? value.ToString()) + " v";
    internal void Open(
        UiElementInstance owner,
        UiDataSource source
    ) {
        Option[] options = Options(owner.definition, source); if (options.Length == 0) return;
        m_owner = owner; error = m_pressed = m_query = string.Empty;
        m_selected = source.TryRead(owner.definition.binding, owner.row, out NovelValue value) ? Array.FindIndex(options, option => option.enabled && option.value.Equals(value)) : -1;
        if (m_selected < 0) m_selected = Array.FindIndex(options, static option => option.enabled);
        m_selectedId = m_selected >= 0 ? options[m_selected].id : string.Empty;
        m_first = Math.Max(0, m_selected);
    }
    internal void Validate(IReadOnlyList<UiElementInstance> order)
    {
        if (m_owner is not null && (!m_owner.inputVisible || !m_owner.enabled || !order.Contains(m_owner))) Close();
    }
    internal IReadOnlyList<UiDrawItem> Build(
        float width,
        float height,
        UiDataSource source
    ) {
        if (m_owner is null) return [];
        Option[] all = Options(m_owner.definition, source);
        if (all.Length == 0) { Close(); return []; }
        Option[] options = Matching(all);
        int previous = m_selected;
        m_selected = Array.FindIndex(options, option => option.id == m_selectedId);
        UiStyleDefinition style = m_owner.definition.style;
        bool search = all.Length > 8 || m_query.Length > 0;
        float rowHeight = Math.Min(height / (search ? 2 : 1), Math.Max(44, style.fontSize * style.lineSpacing + style.textPadding * 2));
        float header = search ? rowHeight : 0;
        m_visible = Math.Clamp((int)Math.Floor((height - header - 32) / rowHeight), 1, Math.Min(8, Math.Max(1, options.Length)));
        m_first = Math.Clamp(m_first, 0, Math.Max(0, options.Length - m_visible));
        if (m_selected != previous && m_selected >= 0) Reveal();
        float popupWidth = Math.Min(width, Math.Max(240, m_owner.visualBounds.width)), popupHeight = header + m_visible * rowHeight;
        float x = Math.Clamp(m_owner.visualBounds.x, 0, width - popupWidth), bottom = m_owner.visualBounds.y + m_owner.visualBounds.height;
        float y = bottom + popupHeight <= height ? bottom : Math.Max(0, m_owner.visualBounds.y - popupHeight);
        m_bounds = new(x, y, popupWidth, popupHeight);
        m_rows = new(x, y + header, popupWidth, m_visible * rowHeight);
        var items = new List<UiDrawItem>();
        if (search)
            items.Add(new(m_owner.id + "/search", UiElementKind.Text, new(x, y, popupWidth, header), m_bounds, style.background, style.foreground,
                source.Localize("ui/selector/search", "输入以筛选") + ": " + m_query, m_owner.definition.font, style.fontSize, style.textPadding, null, false, style.focus));
        if (options.Length == 0)
            items.Add(new(m_owner.id + "/no-options", UiElementKind.Text, m_rows, m_bounds, style.disabled, style.foreground,
                source.Localize("ui/selector/no-results", "没有匹配项"), m_owner.definition.font, style.fontSize, style.textPadding, null, false, style.focus));
        for (int index = m_first; index < Math.Min(options.Length, m_first + m_visible); index++)
        {
            Option option = options[index]; Rect bounds = new(x, m_rows.y + (index - m_first) * rowHeight, popupWidth, rowHeight);
            uint fill = !option.enabled ? style.disabled : index == m_selected ? style.hover : style.background;
            items.Add(new(m_owner.id + "/option/" + option.id, UiElementKind.Button, bounds, m_bounds, fill, style.foreground,
                option.text, m_owner.definition.font, style.fontSize, style.textPadding, null, index == m_selected, style.focus));
        }
        if (error.Length > 0)
        {
            float errorY = y + popupHeight + rowHeight <= height ? y + popupHeight : Math.Max(0, y - rowHeight);
            items.Add(new(m_owner.id + "/binding-error", UiElementKind.Text, new(x, errorY, popupWidth, rowHeight), new(0, 0, width, height), 0, 0xFFAFAFFF,
                error, m_owner.definition.font, style.fontSize, 4, null, false, style.focus));
        }
        return items;
    }
    internal UiInputResult Handle(
        UiInputSnapshot input,
        UiDataSource source
    ) {
        if (m_owner is null) return new(false, []);
        if (input.composing) return new(true, [], input.cancel);
        Option[] all = Options(m_owner.definition, source);
        if (all.Length == 0) { Close(); return new(true, [], input.cancel); }
        if (input.cancel && input.text.Count == 0)
        {
            if (m_query.Length == 0) Close();
            else Filter(string.Empty, all);
            return new(true, [], true);
        }
        string query = m_query;
        if (input.backspace && query.Length > 0) query = query[..StringInfo.ParseCombiningCharacters(query)[^1]];
        if (input.delete || input.selectAll) query = string.Empty;
        foreach (string committed in input.text)
        {
            if (string.IsNullOrEmpty(committed)) continue;
            query += string.Concat(committed.Where(static character => !char.IsControl(character)));
            int[] clusters = StringInfo.ParseCombiningCharacters(query);
            if (clusters.Length > 64) query = query[..clusters[64]];
        }
        bool editing = query != m_query || input.text.Count > 0 || input.backspace || input.delete || input.selectAll;
        if (query != m_query) Filter(query, all);
        if (editing) return new(true, [], input.cancel);
        Option[] options = Matching(all);
        m_selected = Array.FindIndex(options, option => option.id == m_selectedId);
        if (!editing && options.Length > 0 && (input.home || input.end || input.pageUp || input.pageDown))
        {
            int direction = input.home || input.pageUp ? -1 : 1;
            int target = input.home ? 0 : input.end ? options.Length - 1 : Math.Clamp(m_selected + direction * Math.Max(1, m_visible), 0, options.Length - 1);
            for (int index = target; index >= 0 && index < options.Length; index += input.home || input.end ? -direction : direction)
                if (options[index].enabled) { Select(index, options); break; }
        }
        else if (!editing && options.Length > 0 && (input.next || input.previous))
        {
            int current = m_selected < 0 ? input.previous ? 0 : -1 : m_selected;
            for (int count = 0; count < options.Length; count++)
            {
                current = (current + (input.previous ? -1 : 1) + options.Length) % options.Length;
                if (!options[current].enabled) continue;
                Select(current, options); break;
            }
        }
        if (input.scroll != 0) m_first = Math.Clamp(m_first - Math.Sign(input.scroll), 0, Math.Max(0, options.Length - m_visible));
        int hit = input.pointerInside && m_rows.Contains(input.x, input.y) ? m_first + (int)((input.y - m_rows.y) / (m_rows.height / m_visible)) : -1;
        if (input.pressed)
        {
            if (hit < 0 || hit >= options.Length)
            {
                m_pressed = string.Empty;
                if (!input.pointerInside || !m_bounds.Contains(input.x, input.y)) Close();
                return new(true, []);
            }
            m_selected = hit; m_selectedId = options[hit].id; m_pressed = options[hit].enabled ? options[hit].id : string.Empty;
        }
        if (input.released)
        {
            if (hit >= 0 && hit < options.Length && m_pressed == options[hit].id) Commit(options[hit]);
            m_pressed = string.Empty;
        }
        else if (!editing && (input.confirm || input.textConfirm || input.space) && m_selected >= 0) Commit(options[m_selected]);
        return new(true, []);

        void Commit(Option option)
        {
            if (!option.enabled || m_owner is null) return;
            try
            {
                if (source.TryWrite(m_owner.definition.binding, option.value.Copy(), m_owner.row)) { Close(); return; }
                error = source.Localize("ui/selector/rejected", "绑定拒绝此值") + ": " + m_owner.id + " → " + m_owner.definition.binding;
            }
            catch (InvalidOperationException failure) { error = m_owner.id + " → " + m_owner.definition.binding + ": " + failure.Message; }
        }
    }
    private Option[] Matching(Option[] options) => m_query.Length == 0 ? options : options.Where(option => option.text.Contains(m_query, StringComparison.OrdinalIgnoreCase)
        || option.id.Contains(m_query, StringComparison.OrdinalIgnoreCase)).ToArray();
    private void Filter(
        string query,
        Option[] all
    ) {
        m_query = query; m_first = 0; m_pressed = error = string.Empty;
        Option[] options = Matching(all);
        int selected = Array.FindIndex(options, option => option.id == m_selectedId && option.enabled);
        if (selected < 0) selected = Array.FindIndex(options, static option => option.enabled);
        m_selected = selected; m_selectedId = selected < 0 ? string.Empty : options[selected].id;
        Reveal();
    }
    private void Select(
        int index,
        Option[] options
    ) {
        m_selected = index; m_selectedId = options[index].id; m_pressed = string.Empty; Reveal();
    }
    private void Reveal()
    {
        if (m_selected < m_first) m_first = m_selected;
        if (m_selected >= m_first + m_visible) m_first = m_selected - m_visible + 1;
    }
    internal void Close() { m_owner = null; m_pressed = m_query = string.Empty; error = string.Empty; }
}

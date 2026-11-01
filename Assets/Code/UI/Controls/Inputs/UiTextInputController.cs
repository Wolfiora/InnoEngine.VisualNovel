using System;
using System.Globalization;
using System.Text;
using InnoEngine.Mathematics;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.UI.Documents;
using Inno.VisualNovel.UI.Input;

namespace Inno.VisualNovel.UI.Controls.Inputs;

internal sealed class UiTextInputController
{
    private string m_text = string.Empty;
    private int[] m_starts = [];
    private int m_caret, m_anchor, m_viewStart, m_viewEnd;
    private float m_alignment;
    private bool m_initialized;
    private readonly UiElementDefinition m_measurement = new() { kind = UiElementKind.Input,
        layout = new() { width = 1_000_000_000, height = 1_000_000_000 }, style = new() { textPadding = 0, alignment = UiTextAlignment.Left } };
    internal UiElementDefinition Measurement(UiElementDefinition source)
    {
        m_measurement.font = source.font; m_measurement.style.fontSize = source.style.fontSize; m_measurement.style.lineSpacing = source.style.lineSpacing;
        return m_measurement;
    }
    internal void Synchronize(string value)
    {
        if (m_initialized && value == m_text) return;
        m_initialized = true; m_text = value; m_starts = StringInfo.ParseCombiningCharacters(value);
        m_caret = m_anchor = m_starts.Length; m_viewStart = m_viewEnd = 0;
    }
    internal bool Edit(
        UiInputSnapshot input,
        int maximum,
        Func<string, bool> write
    ) {
        bool consumed = input.composing || input.text.Count > 0 || input.confirm || input.textConfirm || input.space;
        if (!input.composing)
        {
            bool left = input.textLeft && !input.textRight, right = input.textRight && !input.textLeft;
            consumed |= input.textLeft || input.textRight || input.home || input.end || input.selectAll || input.delete || input.backspace;
            if (input.selectAll) { m_anchor = 0; m_caret = m_starts.Length; }
            else if (left || right || input.home || input.end)
            {
                if (input.home) m_caret = 0;
                else if (input.end) m_caret = m_starts.Length;
                else if (!input.selecting && m_anchor != m_caret) m_caret = left ? Math.Min(m_anchor, m_caret) : Math.Max(m_anchor, m_caret);
                else m_caret = left ? input.wordNavigation ? WordLeft(m_caret) : Math.Max(0, m_caret - 1)
                    : input.wordNavigation ? WordRight(m_caret) : Math.Min(m_starts.Length, m_caret + 1);
                if (!input.selecting) m_anchor = m_caret;
            }
            if (input.backspace || input.delete)
            {
                int first = Math.Min(m_caret, m_anchor), end = Math.Max(m_caret, m_anchor);
                if (first == end)
                    if (input.backspace) first = input.wordNavigation ? WordLeft(first) : Math.Max(0, first - 1);
                    else end = input.wordNavigation ? WordRight(end) : Math.Min(m_starts.Length, end + 1);
                Replace(first, end, string.Empty, maximum, write);
            }
        }
        foreach (string commit in input.text)
        {
            if (string.IsNullOrEmpty(commit)) continue;
            var clean = new StringBuilder(commit.Length);
            foreach (char character in commit) if (!char.IsControl(character)) clean.Append(character);
            if (clean.Length > 0) Replace(Math.Min(m_caret, m_anchor), Math.Max(m_caret, m_anchor), clean.ToString(), maximum, write);
        }
        return consumed;
    }
    private void Replace(
        int first,
        int end,
        string insert,
        int maximum,
        Func<string, bool> write
    ) {
        string head = m_text[..Offset(first)], tail = m_text[Offset(end)..];
        string candidate = head + insert + tail;
        int[] candidateStarts = StringInfo.ParseCombiningCharacters(candidate);
        if (insert.Length > 0 && candidateStarts.Length > maximum)
        {
            int budget = Math.Max(0, maximum - (m_starts.Length - (end - first)));
            int[] inserted = StringInfo.ParseCombiningCharacters(insert);
            if (budget < inserted.Length) insert = insert[..inserted[budget]];
            candidate = head + insert + tail; candidateStarts = StringInfo.ParseCombiningCharacters(candidate);
            if (candidateStarts.Length > maximum) return;
        }
        if (candidate != m_text && !write(candidate)) return;
        m_text = candidate; m_starts = candidateStarts;
        int offset = head.Length + insert.Length;
        int boundary = Array.BinarySearch(m_starts, offset);
        m_caret = m_anchor = offset >= m_text.Length ? m_starts.Length : boundary >= 0 ? boundary : ~boundary;
    }
    private int Offset(int cluster) => cluster >= m_starts.Length ? m_text.Length : m_starts[Math.Max(0, cluster)];
    private string Slice(
        int first,
        int end
    ) => m_text[Offset(first)..Offset(end)];
    private int WordLeft(int index)
    {
        while (index > 0 && Class(index - 1) == 0) index--;
        if (index == 0) return 0;
        int category = Class(index - 1);
        do { index--; } while (index > 0 && Class(index - 1) == category);
        return index;
    }
    private int WordRight(int index)
    {
        if (index < m_starts.Length && Class(index) != 0)
        { int category = Class(index); do { index++; } while (index < m_starts.Length && Class(index) == category); }
        while (index < m_starts.Length && Class(index) == 0) index++;
        return index;
    }
    private int Class(int cluster)
    {
        int index = m_starts[cluster];
        if (char.IsWhiteSpace(m_text, index)) return 0;
        return CharUnicodeInfo.GetUnicodeCategory(m_text, index) is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter
            or UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter or UnicodeCategory.DecimalDigitNumber
            or UnicodeCategory.LetterNumber or UnicodeCategory.OtherNumber or UnicodeCategory.ConnectorPunctuation ? 1 : 2;
    }
    internal void Point(
        float x,
        float available,
        Func<string, (float width, float height)> measure,
        bool extend,
        bool dragging
    ) {
        x -= m_alignment;
        int index;
        if (dragging && x < 0 && m_viewStart > 0) index = m_viewStart - 1;
        else if (dragging && x > available && m_viewEnd < m_starts.Length) index = m_viewEnd + 1;
        else
        {
            int low = m_viewStart, high = m_viewEnd;
            while (low < high)
            { int middle = (low + high) / 2; if (Width(measure, Slice(m_viewStart, middle)) < x) low = middle + 1; else high = middle; }
            index = low;
            if (index > m_viewStart && x - Width(measure, Slice(m_viewStart, index - 1)) < Width(measure, Slice(m_viewStart, index)) - x) index--;
        }
        m_caret = index; if (!extend) m_anchor = index;
    }
    internal (string text, UiTextInputPresentation presentation) Paint(
        Rect bounds,
        float padding,
        float lineHeight,
        UiTextAlignment alignment,
        Func<string, (float width, float height)> measure,
        bool focused,
        bool retainView
    ) {
        float available = Math.Max(0, bounds.width - padding * 2);
        int viewStart = focused ? Math.Clamp(m_viewStart, 0, m_caret) : 0;
        if (focused)
        {
            int low = viewStart, high = m_caret;
            while (low < high)
            { int middle = (low + high) / 2; if (Width(measure, Slice(middle, m_caret)) <= available) high = middle; else low = middle + 1; }
            viewStart = low;
            if (viewStart == m_starts.Length && m_starts.Length > 0 && available > 0) viewStart--;
        }
        int first = viewStart, end = m_starts.Length;
        while (first < end)
        { int middle = first + (end - first + 1) / 2; if (Width(measure, Slice(viewStart, middle)) <= available) first = middle; else end = middle - 1; }
        int viewEnd = first;
        if (viewEnd == viewStart && viewStart < m_starts.Length && available > 0) viewEnd++;
        string text = Slice(viewStart, viewEnd);
        float width = Width(measure, text), free = Math.Max(0, available - width);
        float aligned = alignment == UiTextAlignment.Center ? free / 2 : alignment == UiTextAlignment.Right ? free : 0;
        float left = bounds.x + padding + aligned, top = bounds.y + padding;
        float height = Math.Clamp(lineHeight, 0, Math.Max(0, bounds.height - padding * 2));
        Rect? selection = null, caret = null;
        if (focused && available > 0 && height > 0)
        {
            int start = Math.Clamp(Math.Min(m_anchor, m_caret), viewStart, viewEnd), finish = Math.Clamp(Math.Max(m_anchor, m_caret), viewStart, viewEnd);
            if (finish > start)
            {
                float a = Math.Clamp(Width(measure, Slice(viewStart, start)), 0, available), b = Math.Clamp(Width(measure, Slice(viewStart, finish)), 0, available);
                if (b > a) selection = new(left + a, top, b - a, height);
            }
            float x = Math.Clamp(Width(measure, Slice(viewStart, Math.Clamp(m_caret, viewStart, viewEnd))), 0, Math.Max(0, available - 1.5f));
            caret = new(left + x, top, Math.Min(1.5f, available), height);
        }
        if (retainView) { m_viewStart = viewStart; m_viewEnd = viewEnd; m_alignment = aligned; }
        return (text, new(selection, caret));
    }
    private static float Width(
        Func<string, (float width, float height)> measure,
        string text
    ) {
        float value = text.Length == 0 ? 0 : measure(text).width;
        if (!float.IsFinite(value) || value < 0) throw new InvalidOperationException("Text input measurement must produce finite nonnegative widths.");
        return value;
    }
}

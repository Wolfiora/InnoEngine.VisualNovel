using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Values;
using InnoEditor.ImGui;

namespace Inno.VisualNovel.Editor.Inspection;

internal sealed class StoryPasteVariablesView(StoryInspectorFields fields)
{
    private VariableDefinition[] m_source = [];
    private readonly Dictionary<string, string> m_modes = new(StringComparer.Ordinal), m_values = new(StringComparer.Ordinal);
    internal void Capture(StoryClipboardReview review) { m_source = review.CopyVariables(); Reset(); }
    internal void Reset()
    {
        m_modes.Clear(); m_values.Clear();
        foreach (var source in m_source) { m_modes.Add(source.id, "keep"); m_values.Add(source.id, string.Empty); }
    }
    internal bool Conflicts(IReadOnlyDictionary<string, VariableDefinition> destination) => m_source.Any(source =>
        destination.TryGetValue(source.id, out var existing) && !StoryVariableDeclarations.Equivalent(source, existing));
    internal void Draw(IReadOnlyDictionary<string, VariableDefinition> destination)
    {
        foreach (var source in m_source)
        {
            ImGui.SeparatorText(fields.Localize("变量 · ") + source.name + " / " + source.id);
            EditorUiSession.current.widgets.Hint(source.scope + " · " + source.initialValue.kind + fields.Localize(" · 默认 ") + Preview(source.initialValue));
            string mode = m_modes[source.id], value = m_values[source.id];
            var compatible = destination.Values.Where(target => target.initialValue.kind == source.initialValue.kind
                && (source.initialValue.kind != NovelValueKind.List || target.initialValue.elementKind == source.initialValue.elementKind)).ToArray();
            if (fields.Select("paste/variable-mode/" + source.id, fields.Localize("声明处理"), ref mode,
                new[] { ("keep", fields.Localize("保留原身份并导入 / 严格匹配")), ("existing", fields.Localize("明确复用目标声明")), ("new", fields.Localize("导入为新变量")) }))
            {
                m_modes[source.id] = mode;
                value = mode == "new" ? source.id + "_copy" : mode == "existing" ? compatible.FirstOrDefault(target => target.id == source.id)?.id ?? compatible.FirstOrDefault()?.id ?? string.Empty : string.Empty;
            }
            if (mode == "existing")
            {
                fields.Select("paste/variable-target/" + source.id, fields.Localize("目标变量"), ref value,
                    new[] { (string.Empty, fields.Localize("选择相同类型的目标声明")) }.Concat(compatible.Select(static variable => (variable.id, variable.name + " · " + variable.id + " / " + variable.scope))));
                if (destination.TryGetValue(value, out var target)) EditorUiSession.current.widgets.Hint(fields.Localize("保留目标作用域和默认值：") + target.scope + " · " + Preview(target.initialValue));
            }
            else if (mode == "new") fields.Text("paste/new-variable/" + source.id, fields.Localize("新变量身份"), ref value);
            else if (destination.TryGetValue(source.id, out var existing) && !StoryVariableDeclarations.Equivalent(source, existing))
                EditorUiSession.current.widgets.Hint(fields.Localize("同名声明存在差异；请选择明确复用目标或导入为新变量，原声明不会被覆盖。"));
            m_values[source.id] = value;
        }
    }
    internal IReadOnlyDictionary<string, string> Mappings()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in m_source)
            if (m_modes[source.id] != "keep")
            {
                string value = m_values[source.id];
                if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException(fields.Localize("为复制变量选择目标或新身份：") + source.name);
                result.Add(source.id, value);
            }
        return result;
    }
    private static string Preview(NovelValue value)
    {
        if (value.kind == NovelValueKind.List) return "列表<" + value.elementKind + ">";
        string text = value.ToString(); var elements = StringInfo.GetTextElementEnumerator(text); int length = 0;
        for (int index = 0; index < 32 && elements.MoveNext(); index++) length = elements.ElementIndex + elements.GetTextElement().Length;
        return text[..length].Replace('\r', ' ').Replace('\n', ' ') + (length < text.Length ? "…" : string.Empty);
    }
}

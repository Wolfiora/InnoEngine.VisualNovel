using System.Globalization;
using System;
using System.Collections.Generic;
using InnoEditor.Interactions;

namespace Inno.VisualNovel.Editor.Localization;

internal sealed class EditorTextView(EditorInteractions interactions)
{
    private readonly EditorTextCatalog m_source = new(null);
    private EditorTextCatalog? catalog => interactions.TryGetModule<NovelEditorLanguageModule>(out var owner) ? owner?.catalog : null;
    internal string language => catalog?.language ?? "zh-CN";
    internal string error => interactions.TryGetModule<NovelEditorLanguageModule>(out var owner) ? owner?.lastError ?? string.Empty : string.Empty;
    internal string Localize(string source) => catalog?.Text(source) ?? source;
    internal string Error(Exception error) => (catalog ?? m_source).Error(error);
    internal IEnumerable<(string id, string name)> BuiltIn(IEnumerable<(string id, string name)> choices) => (catalog ?? m_source).BuiltInChoices(choices);
    internal string Label(string source) => (catalog ?? m_source).Label(source);
    internal string Caption(
        string source,
        string translated
    ) => (catalog ?? m_source).Label(source, translated);
    internal string Format(
        string source,
        params object?[] arguments
    ) => catalog?.Format(source, arguments) ?? string.Format(CultureInfo.InvariantCulture, source, arguments);
}

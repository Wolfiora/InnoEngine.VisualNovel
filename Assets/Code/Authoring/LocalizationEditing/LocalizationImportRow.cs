using Inno.VisualNovel.Model.Localization;

namespace Inno.VisualNovel.Authoring.LocalizationEditing;

/// <summary>Describes one immutable, reviewable translation exchange row.</summary>
/// <param name="id">Stable text identity.</param>
/// <param name="source">Source carried by the exchange file.</param>
/// <param name="language">Declared target tag.</param>
/// <param name="text">Translated text.</param>
/// <param name="status">Requested review state.</param>
/// <param name="fingerprint">Exact approved source revision.</param>
/// <param name="voice">Optional voice cue.</param>
/// <param name="message">Conflict, stale-source or validation explanation.</param>
/// <param name="applicable">Whether explicit application may include this row.</param>
public sealed record LocalizationImportRow(string id, string source, string language, string text, TranslationStatus status, string fingerprint, string voice, string message, bool applicable);

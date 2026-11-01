using System.Collections.Generic;

namespace Inno.VisualNovel.Authoring.LocalizationEditing;

/// <summary>Retains a source-bound import preview without editing the native document.</summary>
/// <param name="baseline">The table content digest at preview time.</param>
/// <param name="rows">Immutable decisions and diagnostics.</param>
public sealed record LocalizationImportPreview(string baseline, IReadOnlyList<LocalizationImportRow> rows);

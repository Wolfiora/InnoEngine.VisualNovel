using System.Collections.Generic;

namespace Inno.VisualNovel.Authoring.StoryEditing;

/// <summary>Freezes a dialogue exchange review against one chapter and resource-library baseline.</summary>
/// <param name="baseline">The native chapter content digest.</param>
/// <param name="resources">The reviewed character and voice metadata digest.</param>
/// <param name="rows">Immutable reviewed records; any error blocks the entire import.</param>
public sealed record StoryImportPreview(
    string baseline,
    string resources,
    IReadOnlyList<StoryImportRow> rows
);

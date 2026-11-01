using System;

namespace Inno.VisualNovel.Authoring.Documents;

/// <summary>Describes one open native document without reading or decoding its authored bytes.</summary>
/// <param name="documentId">The engine document identity in the current editor generation.</param>
/// <param name="path">The engine source path retained for diagnostics.</param>
/// <param name="revision">The owner-local change counter, including undo and redo.</param>
/// <param name="unsaved">Whether the engine document currently contains unsaved changes.</param>
public sealed record NativeDocumentStamp(
    Guid documentId,
    string path,
    long revision,
    bool unsaved
);

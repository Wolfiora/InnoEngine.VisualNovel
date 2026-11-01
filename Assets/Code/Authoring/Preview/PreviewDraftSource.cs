using System;

namespace Inno.VisualNovel.Authoring.Preview;

/// <summary>Records one native resource generation and whether its open dirty document supplied preview content.</summary>
/// <param name="assetId">The canonical persistent resource identity.</param>
/// <param name="path">The native source location suitable for diagnostics.</param>
/// <param name="contentVersion">The captured native resource generation.</param>
/// <param name="unsaved">Whether the shared document supplied unsaved source values.</param>
public sealed record PreviewDraftSource(
    Guid assetId,
    string path,
    long contentVersion,
    bool unsaved
);

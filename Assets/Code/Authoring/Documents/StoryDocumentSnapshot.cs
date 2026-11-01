using System;
using InnoEngine.Assets;

namespace Inno.VisualNovel.Authoring.Documents;

/// <summary>Describes one detached authored chapter without opening, saving or changing the active engine document.</summary>
/// <param name="path">The native source location.</param>
/// <param name="draft">The independently owned current authored graph.</param>
/// <param name="documentId">The existing open document identity, or empty for an unopened source.</param>
/// <param name="unsaved">Whether the snapshot includes unsaved native document edits.</param>
/// <param name="fingerprint">The exact neutral draft fingerprint used to detect stale search results.</param>
public sealed record StoryDocumentSnapshot(AssetPath path, StoryDraftState draft, Guid documentId, bool unsaved, string fingerprint);

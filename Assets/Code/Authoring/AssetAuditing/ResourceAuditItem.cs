using System;
using System.Collections.Generic;
using InnoEngine.Assets;

namespace Inno.VisualNovel.Authoring.AssetAuditing;

/// <summary>Distinguishes proven selected-game closure membership from unavailable evidence and editor-only sources.</summary>
public enum ResourceAuditUse
{
    /// <summary>The native closure or source identity is unavailable.</summary>
    Unknown,
    /// <summary>The selected game's explicit native dependency closure contains this identity.</summary>
    Referenced,
    /// <summary>A complete selected-game closure does not contain this imported resource; this is not proof about other games.</summary>
    NotReferenced,
    /// <summary>The source is an authoring catalog or test that is intentionally outside Player content.</summary>
    AuthoringOnly
}

/// <summary>Contains an immutable source inventory observation without owning or registering an asset.</summary>
/// <param name="id">The engine persistent identity, empty when unverified.</param>
/// <param name="path">The project source path.</param>
/// <param name="bytes">The observed source-file size.</param>
/// <param name="hash">SHA-256 of unchanged source bytes, empty when incomplete or changed.</param>
/// <param name="use">Membership in the selected game's native closure.</param>
/// <param name="referrers">Direct native closure parent locations.</param>
/// <param name="license">Frozen author-supplied provenance, when present.</param>
/// <param name="message">An explanation of unavailable evidence or source changes.</param>
public sealed record ResourceAuditItem(Guid id, AssetPath path, long bytes, string hash, ResourceAuditUse use,
    IReadOnlyList<string> referrers, ResourceLicenseInfo? license, string message);

/// <summary>Freezes provenance without keeping native source objects in audit output.</summary>
/// <param name="assetId">The engine resource identity.</param>
/// <param name="author">The recorded creator.</param>
/// <param name="source">The recorded origin.</param>
/// <param name="license">The stated license identifier.</param>
/// <param name="redistributionConfirmed">Whether the author has confirmed the intended distribution.</param>
/// <param name="attributionRequired">Whether a notice is required.</param>
/// <param name="notice">The supplied notice text.</param>
/// <param name="modifications">The recorded modifications.</param>
public sealed record ResourceLicenseInfo(Guid assetId, string author, string source, string license, bool redistributionConfirmed,
    bool attributionRequired, string notice, string modifications);

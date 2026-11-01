using System;

namespace Inno.VisualNovel.Authoring.CharacterEditing.ImageImport;

/// <summary>Freezes one image's reviewed destination without exposing mutable character candidates.</summary>
/// <param name="path">The exact native image path.</param>
/// <param name="imageId">The persistent imported image identity.</param>
/// <param name="imageVersion">The reviewed native content generation.</param>
/// <param name="kind">The declared pose or expression destination.</param>
/// <param name="variant">The requested filename variant label.</param>
/// <param name="part">The requested filename part label.</param>
/// <param name="variantId">The resolved existing or freshly allocated variant identity.</param>
/// <param name="partId">The resolved semantic part identity.</param>
/// <param name="createsVariant">Whether this ready row requires a new pose or expression.</param>
/// <param name="applicable">Whether the exact row contributes to the atomic ready subset.</param>
/// <param name="message">The matched, missing or conflicting destination explanation.</param>
public sealed record CharacterImageBatchRow(
    string path,
    Guid imageId,
    long imageVersion,
    CharacterImageBatchKind kind,
    string variant,
    string part,
    string variantId,
    string partId,
    bool createsVariant,
    bool applicable,
    string message
);

using System;

namespace Inno.VisualNovel.Authoring.VoiceManagement;

/// <summary>Describes one reviewed source line and its exact imported audio match.</summary>
/// <param name="node">The stable graph node identity.</param>
/// <param name="textId">The stable dialogue identity used for filename matching.</param>
/// <param name="speaker">The source speaker.</param>
/// <param name="text">The source recording text.</param>
/// <param name="previousCue">The binding present during review.</param>
/// <param name="cue">The proposed reusable Voice cue identity.</param>
/// <param name="clipId">The native persistent clip identity.</param>
/// <param name="clipVersion">The reviewed native content generation.</param>
/// <param name="clipPath">The canonical source location.</param>
/// <param name="applicable">Whether this exact row can be applied.</param>
/// <param name="message">A missing, ambiguous, existing or ready explanation.</param>
public sealed record VoiceBatchRow(
    string node,
    string textId,
    string speaker,
    string text,
    string previousCue,
    string cue,
    Guid clipId,
    long clipVersion,
    string clipPath,
    bool applicable,
    string message
);

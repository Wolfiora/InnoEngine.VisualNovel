namespace Inno.VisualNovel.Narrative.Reading;

/// <summary>Describes one currently visible choice without exposing mutable story data.</summary>
/// <param name="id">The stable option identity.</param>
/// <param name="text">The displayed option text.</param>
/// <param name="enabled">Whether the option accepts input.</param>
/// <param name="disabledReason">An author-facing explanation for disabled options.</param>
/// <param name="selected">Whether this option was selected in the current saved game branch.</param>
public sealed record ChoiceState(
    string id,
    string text,
    bool enabled,
    string disabledReason,
    bool selected = false
);

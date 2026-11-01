using Inno.VisualNovel.Model.Story.Nodes.Reading;

namespace Inno.VisualNovel.Authoring.StoryEditing;

/// <summary>Describes one reviewed dialogue record without retaining source assets.</summary>
/// <param name="record">The one-based logical input record, including a delimited header.</param>
/// <param name="speaker">The optional literal speaker override.</param>
/// <param name="character">The resolved stable character resource identity.</param>
/// <param name="text">The complete dialogue text.</param>
/// <param name="voice">The resolved voice cue identity.</param>
/// <param name="textId">The preserved or newly allocated stable text identity.</param>
/// <param name="note">The author annotation.</param>
/// <param name="presentation">The selected reading layout.</param>
/// <param name="message">The actionable matching or validation error.</param>
public sealed record StoryImportRow(
    int record,
    string speaker,
    string character,
    string text,
    string voice,
    string textId,
    string note,
    DialoguePresentation presentation,
    string message
);

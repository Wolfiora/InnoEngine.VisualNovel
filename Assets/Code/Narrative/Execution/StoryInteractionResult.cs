namespace Inno.VisualNovel.Narrative.Execution;

/// <summary>Identifies a committed player interaction observed by an owner-local route recorder.</summary>
public enum StoryInteractionKind
{
    /// <summary>A visible enabled choice was selected.</summary>
    Choice,
    /// <summary>A valid literal input was committed.</summary>
    InputSubmitted,
    /// <summary>An authored input cancellation output was taken.</summary>
    InputCancelled
}

/// <summary>Describes the latest accepted input without exposing callbacks, subscriptions or mutable game data.</summary>
/// <param name="sequence">The interpreter-local monotonic commit identity.</param>
/// <param name="kind">The accepted interaction kind.</param>
/// <param name="chapterId">The authored source chapter.</param>
/// <param name="nodeId">The authored source node.</param>
/// <param name="value">The stable option identity or literal submitted input.</param>
public sealed record StoryInteractionResult(long sequence, StoryInteractionKind kind, string chapterId, string nodeId, string value);

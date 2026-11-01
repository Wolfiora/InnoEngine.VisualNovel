namespace Inno.VisualNovel.Authoring.Preview;

/// <summary>Defines the explicit starting semantics of an isolated authoring preview.</summary>
public enum PreviewRunMode
{
    /// <summary>Starts the real project entry with optional explicit test values.</summary>
    Complete,
    /// <summary>Starts a selected node with declared initial values and stage state, without guessing its past.</summary>
    Local,
    /// <summary>Replays recorded decisions from an explicit entry to an interactive target.</summary>
    Route
}

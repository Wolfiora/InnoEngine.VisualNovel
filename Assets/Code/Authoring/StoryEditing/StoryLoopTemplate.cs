namespace Inno.VisualNovel.Authoring.StoryEditing;

/// <summary>Identifies the ordinary graph nodes created for one visual counted-loop template.</summary>
/// <param name="entryNode">The counter initialization node.</param>
/// <param name="bodyNode">The editable dialogue body.</param>
/// <param name="exitNode">The ordinary exit continuation node.</param>
/// <param name="counterVariable">The generated invocation-local counter.</param>
public sealed record StoryLoopTemplate(
    string entryNode,
    string bodyNode,
    string exitNode,
    string counterVariable);

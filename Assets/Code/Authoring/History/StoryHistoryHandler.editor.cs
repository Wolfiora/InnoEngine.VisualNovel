using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.Documents;

namespace Inno.VisualNovel.Authoring.History;

/// <summary>Resolves neutral story history through the current document generation.</summary>
/// <param name="documents">The current document owner.</param>
[EditorHistoryHandler(StoryDocumentsModule.historyKind)]
public sealed class StoryHistoryHandler(StoryDocumentsModule documents) : EditorHistoryHandler
{
    /// <inheritdoc />
    protected override EditorHistoryAvailability Query(
        EditorHistoryContext context,
        EditorHistoryChange change,
        EditorHistoryDirection direction
    ) => documents.QueryHistory(change);

    /// <inheritdoc />
    protected override EditorHistoryResult Apply(
        EditorHistoryContext context,
        EditorHistoryChange change,
        EditorHistoryDirection direction
    ) => documents.ApplyHistory(change, direction);

    /// <inheritdoc />
    protected override bool TryMerge(
        EditorHistoryChange older,
        EditorHistoryChange newer,
        out EditorHistoryChange? merged
    ) => documents.TryMergeHistory(older, newer, out merged);
}

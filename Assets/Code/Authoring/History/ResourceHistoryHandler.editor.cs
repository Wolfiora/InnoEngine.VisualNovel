using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.Documents;

namespace Inno.VisualNovel.Authoring.History;

/// <summary>Resolves presentation resource history through the current generation's document owner.</summary>
/// <param name="documents">The active resource document owner.</param>
[EditorHistoryHandler(ResourceDocumentsModule.historyKind)]
public sealed class ResourceHistoryHandler(ResourceDocumentsModule documents) : EditorHistoryHandler
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

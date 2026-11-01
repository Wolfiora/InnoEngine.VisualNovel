namespace Inno.VisualNovel.Authoring.UiEditing;

/// <summary>Describes an exact subtree destination resolved against one native document revision.</summary>
/// <param name="sourceId">The existing subtree identity.</param>
/// <param name="parentId">The destination parent.</param>
/// <param name="index">The insertion index after removing the source.</param>
/// <param name="changesParent">Whether absolute coordinates may acquire another layout and clip owner.</param>
/// <param name="changesRowContext">Whether item bindings acquire another repeating ancestor.</param>
public sealed record UiHierarchyDropTarget(string sourceId, string parentId, int index, bool changesParent, bool changesRowContext);

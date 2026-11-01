using System;
using System.Linq;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Model.Story;

namespace Inno.VisualNovel.Authoring.StoryEditing;

/// <summary>Edits chapter structure without inferring execution from fragment display order.</summary>
public sealed class StoryStructureService
{
    /// <summary>Changes a chapter's display title without changing its content identity.</summary>
    /// <param name="draft">The candidate.</param>
    /// <param name="title">The display title.</param>
    public void SetTitle(
        StoryDraftState draft,
        string title
    ) => draft.title = title ?? throw new ArgumentNullException(nameof(title));

    /// <summary>Adds an empty authoring group with a fresh stable identity.</summary>
    /// <param name="draft">The candidate.</param>
    /// <param name="name">The display name.</param>
    /// <returns>The new fragment identity.</returns>
    public string AddFragment(
        StoryDraftState draft,
        string name
    ) {
        string id = Guid.NewGuid().ToString("N");
        draft.fragments = [.. draft.fragments, new() { id = id, name = name }];
        return id;
    }

    /// <summary>Renames a display group without changing references.</summary>
    /// <param name="draft">The candidate.</param>
    /// <param name="id">The fragment identity.</param>
    /// <param name="name">The new display label.</param>
    public void RenameFragment(
        StoryDraftState draft,
        string id,
        string name
    ) => draft.fragments.Single(item => item.id == id).name = name;

    /// <summary>Moves node ownership without altering any edge or public entry.</summary>
    /// <param name="draft">The candidate.</param>
    /// <param name="nodeId">The existing node.</param>
    /// <param name="fragmentId">The destination group.</param>
    public void MoveToFragment(
        StoryDraftState draft,
        string nodeId,
        string fragmentId
    ) {
        if (draft.graph.document.FindNode(new(nodeId)) is null) throw new ArgumentException("Node is missing.", nameof(nodeId));
        StoryFragment destination = draft.fragments.Single(item => item.id == fragmentId);
        foreach (StoryFragment fragment in draft.fragments) fragment.nodes = fragment.nodes.Where(item => item != nodeId).ToArray();
        destination.nodes = [.. destination.nodes, nodeId];
    }

    /// <summary>Removes an empty group; populated groups require moving their nodes first.</summary>
    /// <param name="draft">The candidate.</param>
    /// <param name="fragmentId">The group identity.</param>
    public void RemoveFragment(
        StoryDraftState draft,
        string fragmentId
    ) {
        StoryFragment fragment = draft.fragments.Single(item => item.id == fragmentId);
        if (fragment.nodes.Length > 0 || draft.fragments.Length == 1) throw new InvalidOperationException("Keep one fragment and move its nodes before deleting a group.");
        draft.fragments = draft.fragments.Where(item => item.id != fragmentId).ToArray();
    }

    /// <summary>Adds or retargets an explicitly named public entry.</summary>
    /// <param name="draft">The candidate.</param>
    /// <param name="entryId">The stable public name.</param>
    /// <param name="nodeId">The existing target.</param>
    public void SetEntry(
        StoryDraftState draft,
        string entryId,
        string nodeId
    ) {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryId);
        if (draft.graph.document.FindNode(new(nodeId)) is null) throw new ArgumentException("Entry target is missing.", nameof(nodeId));
        StoryEntry? entry = draft.entries.SingleOrDefault(item => item.id == entryId);
        if (entry is null) draft.entries = [.. draft.entries, new() { id = entryId, nodeId = nodeId }];
        else entry.nodeId = nodeId;
    }
}

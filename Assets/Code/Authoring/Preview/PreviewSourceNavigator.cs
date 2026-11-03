using System;
using System.Collections.Generic;
using InnoEngine.Assets;
using Inno.VisualNovel.Authoring.Diagnostics;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Model.Project;

namespace Inno.VisualNovel.Authoring.Preview;

/// <summary>Resolves source-qualified debugger locations through native chapter references and existing unsaved documents.</summary>
public static class PreviewSourceNavigator
{
    /// <summary>Preflights an exact source chapter and node without opening documents or changing selection.</summary>
    /// <param name="context">The explicitly selected game whose native chapter library bounds the lookup.</param>
    /// <param name="stories">The existing shared story document owner.</param>
    /// <param name="chapter">The original authored chapter identity from the production source map.</param>
    /// <param name="node">The original authored node identity, including Missing implementations.</param>
    /// <param name="primarySource">An explicitly captured local-preview source outside the ordinary game library, never an inferred active document.</param>
    /// <returns>A detached native snapshot for a guarded source-navigation action.</returns>
    public static StoryDocumentSnapshot Resolve(
        NovelProjectAsset context,
        StoryDocumentsModule stories,
        string chapter,
        string node,
        AssetPath? primarySource = null
    ) {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(stories);
        if (string.IsNullOrWhiteSpace(chapter) || string.IsNullOrWhiteSpace(node))
            throw new AuthoringValidationException($"Debug navigation requires nonempty chapter and node identities.");
        var matches = new List<AssetPath>();
        bool containsPrimary = false;
        foreach (var candidate in context.chapters)
        {
            if (candidate is null || candidate.isMissing || candidate.assetPath.localPath.Length == 0) continue;
            if (primarySource is { } primary && candidate.assetPath.Equals(primary)) containsPrimary = true;
            string identity = candidate.assetPath.source == AssetSourceId.project && stories.TryReadUnsaved(candidate.assetPath, out var dirty) ? dirty!.chapterId : candidate.chapterId;
            if (identity == chapter) matches.Add(candidate.assetPath);
        }
        if (!containsPrimary && primarySource is { } local)
        {
            var snapshot = stories.Inspect(local);
            if (snapshot.draft.chapterId == chapter) matches.Add(local);
        }
        if (matches.Count != 1) throw new AuthoringValidationException($"The game cannot uniquely resolve debug chapter {chapter}. Check chapter references and unsaved identity changes.");
        AssetPath path = matches[0];
        if (path.source != AssetSourceId.project) throw new AuthoringValidationException($"The debug source is read-only plugin content: {path}. Create an editable project copy first.");
        return RequireNode(stories.Inspect(path), chapter, node);
    }
    private static StoryDocumentSnapshot RequireNode(
        StoryDocumentSnapshot snapshot,
        string chapter,
        string node
    ) {
        if (snapshot.draft.chapterId != chapter || snapshot.draft.graph.document.FindNode(new(node)) is null)
            throw new AuthoringValidationException($"The debug node was deleted or does not belong to its source chapter: {chapter}/{node}.");
        return snapshot;
    }
}

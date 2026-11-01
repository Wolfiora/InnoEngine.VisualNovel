using System;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Authoring.Preview;

/// <summary>Projects existing native dirty documents into isolated preview content without saving or mutating canonical assets.</summary>
public static class PreviewDraftProjectBuilder
{
    /// <summary>Begins an editor-thread preparation that yields, reports progress and cancels before replacing a running preview.</summary>
    /// <param name="context">The explicitly chosen imported game root.</param>
    /// <param name="stories">The current generation's shared story documents and production compiler.</param>
    /// <param name="resources">The current generation's shared native resource documents.</param>
    /// <param name="activeVariables">An explicitly previewed chapter library outside the game's ordinary closure, when needed.</param>
    /// <returns>An independent operation; dispose it on cancellation, reload or panel destruction.</returns>
    public static PreviewProjectPreparation Begin(
        NovelProjectAsset context,
        StoryDocumentsModule stories,
        ResourceDocumentsModule resources,
        VariableTableAsset? activeVariables = null
    ) => new(context, stories, resources, activeVariables);
    /// <summary>Prepares the same production candidate synchronously for noninteractive tools and host integrations.</summary>
    /// <param name="context">The explicitly chosen imported game root.</param>
    /// <param name="stories">The shared story documents and compiler.</param>
    /// <param name="resources">The shared native resource documents.</param>
    /// <param name="activeVariables">An optional explicitly previewed chapter library.</param>
    /// <returns>A complete, source-validated candidate; failure leaves existing previews and documents untouched.</returns>
    public static PreviewProjectSnapshot Build(
        NovelProjectAsset context,
        StoryDocumentsModule stories,
        ResourceDocumentsModule resources,
        VariableTableAsset? activeVariables = null
    ) {
        using var operation = Begin(context, stories, resources, activeVariables);
        while (!operation.Advance(1024, 1000)) { }
        return operation.result ?? throw (operation.failure ?? new InvalidOperationException(operation.error));
    }
}

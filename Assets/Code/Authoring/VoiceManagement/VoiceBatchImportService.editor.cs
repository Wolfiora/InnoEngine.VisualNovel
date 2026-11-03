using System;
using System.Linq;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.ProjectEditing;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Localization;

namespace Inno.VisualNovel.Authoring.VoiceManagement;

/// <summary>Coordinates chapter and game-library edits in one engine History transaction.</summary>
public sealed class VoiceBatchImportService(
    StoryDocumentsModule stories,
    ResourceDocumentsModule resources,
    EditorInteractions interactions
)
{
    /// <summary>Prepares native assets and applies a reviewed batch without guessing or overwriting existing bindings.</summary>
    /// <param name="chapterDocument">The shared native chapter document.</param>
    /// <param name="projectDocument">The writable game's shared resource document.</param>
    /// <param name="preview">The exact reviewed batch.</param>
    /// <returns>The number of associated dialogue lines.</returns>
    public int Apply(
        Guid chapterDocument,
        Guid projectDocument,
        VoiceBatchPreview preview
    ) {
        var service = new VoiceBatchService(stories.codec);
        var draft = stories.Read(chapterDocument); NovelProjectAsset project = resources.Read<NovelProjectAsset>(projectDocument);
        Guid localeDocument = Guid.Empty; LocalizationTableAsset? translations = null;
        if (preview.localized)
        {
            var source = project.localization ?? throw new InvalidOperationException("The game has no localization table for this voice batch.");
            localeDocument = resources.Open(source.assetPath).documentId; translations = resources.Read<LocalizationTableAsset>(localeDocument);
        }
        if (!project.chapters.Any(item => item.chapterId == draft.chapterId)) throw new InvalidOperationException("The selected game does not own this chapter.");
        service.ValidateBaseline(draft, preview, translations); VoiceBatchService.ValidateCharacters(project, preview);
        int count = preview.rows.Count(static item => item.applicable);
        if (count == 0) return 0;
        var cues = NativeVoiceImports.PrepareCues(project, preview);
        using EditorHistoryTransaction transaction = interactions.history.BeginTransaction("Bind dialogue voices in batch");
        if (preview.localized) resources.Edit<LocalizationTableAsset>(localeDocument, "Bind localized voices", candidate => service.ApplyLocalized(draft, candidate, preview, cues));
        else stories.Edit(chapterDocument, "Bind dialogue voice", candidate => service.Apply(candidate, preview, cues));
        resources.Edit<NovelProjectAsset>(projectDocument, "Attach voice resources", candidate => { foreach (var cue in cues) NovelProjectEditService.Attach(candidate, cue); });
        transaction.Commit(); return count;
    }
}

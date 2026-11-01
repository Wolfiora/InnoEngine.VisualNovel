using System;
using System.Linq;
using Inno.VisualNovel.Compiler.Validation;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Collections;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Player.Collections;
using Inno.VisualNovel.Player.Sessions;

namespace Inno.VisualNovel.Authoring.AssetImporting;

/// <summary>Validates collection references and rules against the exact game's compiled declarations.</summary>
public static class CollectionProjectValidator
{
    /// <summary>Requires available media, explicit replay entries and compatible initial values.</summary>
    /// <param name="project">The candidate game content closure.</param>
    public static void Validate(NovelProjectAsset project)
    {
        _ = new NovelCollectionService(project.collections);
        var library = new StoryProgramLibrary(project);
        var entries = project.collections?.entries ?? [];
        foreach (var entry in entries)
        {
            CollectionConditionValidator.Validate(entry.unlockCondition, library.program.variables.Where(static variable => variable.scope != VariableScope.Local));
            if (entry.kind == CollectionKind.Cg && entry.image is null) throw new InvalidOperationException("CG artwork is missing: " + entry.id);
            if (entry.kind == CollectionKind.Music && !project.audio.Any(cue => cue.resourceId == entry.musicCueId && cue.channel is NovelAudioChannel.Bgm or NovelAudioChannel.Ambience))
                throw new InvalidOperationException("Collection music requires a BGM or Ambience cue: " + entry.id);
            if (entry.kind != CollectionKind.Replay) continue;
            _ = library.ResolveEntry(entry.chapterId, entry.entryId);
            if (entry.initialValues.Select(static item => item.id).Distinct(StringComparer.Ordinal).Count() != entry.initialValues.Length) throw new InvalidOperationException("Replay initial values contain duplicates: " + entry.id);
            foreach (var initial in entry.initialValues)
            {
                var declaration = library.program.variables.SingleOrDefault(variable => variable.id == initial.id && variable.scope != VariableScope.Global)
                    ?? throw new InvalidOperationException("Replay initial variable is unavailable: " + entry.id + "/" + initial.id);
                VariableValueRules.Validate(declaration, initial.value);
            }
        }
        foreach (var instruction in library.program.instructions)
            if (instruction.unlock is { } unlock && !entries.Any(entry => entry.id == unlock.entryId))
                throw new InvalidOperationException("Unlock target is missing at " + instruction.sourceChapterId + "/" + instruction.sourceNodeId + ": " + unlock.entryId);
    }
}

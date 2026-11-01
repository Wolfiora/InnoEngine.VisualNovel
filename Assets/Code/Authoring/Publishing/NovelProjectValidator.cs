using System;
using System.Linq;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Model.Story.Nodes.Audio;
using Inno.VisualNovel.Model.Story.Nodes.Interface;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Authoring.Diagnostics;
using Inno.VisualNovel.Authoring.AssetImporting;

namespace Inno.VisualNovel.Authoring.Publishing;

/// <summary>Shares complete game content validation between native import and publication preflight.</summary>
public static class NovelProjectValidator
{
    /// <summary>Rejects unavailable content, incompatible instructions and incomplete reading bindings.</summary>
    /// <param name="asset">The detached or imported game definition.</param>
    public static void Validate(NovelProjectAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (asset.manualSaveSlots is < 1 or > 10000 || asset.automaticSaveSlots is < 1 or > 100)
            throw new InvalidOperationException("Manual save capacity must be 1–10000; automatic rotation must be 1–100. The quick slot is independent.");
        if (asset.additionalResources.Any(static item => item is null || item.isMissing || item.identity.persistentId == Guid.Empty)
            || asset.additionalResources.Select(static item => item.identity.persistentId).Distinct().Count() != asset.additionalResources.Length)
            throw new InvalidOperationException("Additional packaging roots must be distinct persistent available native assets.");
        if (string.IsNullOrWhiteSpace(asset.gameId) || asset.chapters.Length == 0 || asset.chapters.Any(static chapter => chapter.isMissing || chapter.program is null)
            || asset.chapters.Select(static chapter => chapter.chapterId).Distinct(StringComparer.Ordinal).Count() != asset.chapters.Length)
            throw new InvalidOperationException("A VN project needs a stable game identity and unique compiled chapters.");
        var start = asset.chapters.SingleOrDefault(chapter => chapter.chapterId == asset.startChapter);
        if (start?.program is null || !start.program.entries.Any(entry => entry.id == asset.startEntry)) throw new InvalidOperationException("The game's initial chapter entry is missing.");
        if (asset.font is null || asset.font.isMissing || asset.material is null || asset.material.isMissing)
            throw new InvalidOperationException("A game requires an imported font and composition material.");
        if (!float.IsFinite(asset.width) || !float.IsFinite(asset.height) || asset.width <= 0 || asset.height <= 0 || asset.width > 16384 || asset.height > 16384 || asset.rollbackLimit is < 1 or > 10000)
            throw new InvalidOperationException("The game design canvas or rollback limit exceeds its declared bounds.");
        if (asset.stages.Any(static item => item.isMissing) || asset.characters.Any(static item => item.isMissing)
            || asset.stages.Select(static item => item.resourceId).Distinct(StringComparer.Ordinal).Count() != asset.stages.Length
            || asset.characters.Select(static item => item.resourceId).Distinct(StringComparer.Ordinal).Count() != asset.characters.Length
            || asset.initialStage.Length > 0 && !asset.stages.Any(item => item.resourceId == asset.initialStage))
            throw new InvalidOperationException("Stage and character libraries require unique keys and available resources.");
        foreach (var character in asset.characters) Model.Characters.CharacterDefinitionRules.Validate(character);
        foreach (var chapter in asset.chapters)
            foreach (var instruction in chapter.program!.instructions)
            {
                if (instruction.dialogue is { characterId.Length: > 0 } line && !asset.characters.Any(item => item.resourceId == line.characterId))
                    throw new InvalidOperationException($"Dialogue character '{line.characterId}' is missing at {chapter.chapterId}/{instruction.nodeId}.");
                if (instruction.stage is { } stage && (stage.operation == StageOperation.SetStage && !asset.stages.Any(item => item.resourceId == stage.resourceId)
                    || stage.operation == StageOperation.ShowCharacter && !asset.characters.Any(item => item.resourceId == stage.resourceId)))
                    throw new InvalidOperationException($"Stage resource '{stage.resourceId}' is missing at {chapter.chapterId}/{instruction.nodeId}.");
                if (instruction.stage is { operation: StageOperation.ShowCharacter } show)
                {
                    var character = asset.characters.Single(item => item.resourceId == show.resourceId);
                    string pose = show.pose.Length == 0 ? character.defaultPose : show.pose;
                    string expression = show.expression.Length == 0 ? character.defaultExpression : show.expression;
                    if (!character.poses.Any(item => item.id == pose)
                        || (character.expressions.Length > 0 || show.expression.Length > 0) && !character.expressions.Any(item => item.id == expression))
                        throw new InvalidOperationException($"Character pose/expression '{pose}/{expression}' is missing at {chapter.chapterId}/{instruction.nodeId} → {character.resourceId}.");
                }
                if (instruction.audio is { operation: AudioOperation.Play } sound && !asset.audio.Any(item => item.resourceId == sound.cueId))
                    throw new InvalidOperationException($"Audio resource '{sound.cueId}' is missing at {chapter.chapterId}/{instruction.nodeId}.");
                if (instruction.performance is { operation: PerformanceOperation.Play } performance && !asset.performances.Any(item => item.resourceId == performance.clipId))
                    throw new InvalidOperationException($"Performance resource '{performance.clipId}' is missing at {chapter.chapterId}/{instruction.nodeId}.");
                if (instruction.stage is { operation: StageOperation.StartParticles } emitter && !asset.particles.Any(item => item.resourceId == emitter.resourceId))
                    throw new InvalidOperationException($"Particle preset '{emitter.resourceId}' is missing at {chapter.chapterId}/{instruction.nodeId}.");
                if (instruction.system is { } action)
                {
                    SystemNodeRules.Validate(action);
                    if (action.operation == SystemOperation.OpenPage && !asset.screens.Any(page => page.role == action.argument)
                        || action.operation == SystemOperation.SetTheme && action.argument.Length > 0 && !asset.themes.Any(theme => theme.resourceId == action.argument))
                        throw new InvalidOperationException($"System page or theme '{action.argument}' is missing at {chapter.chapterId}/{instruction.nodeId}.");
                }
            }
        if (asset.audio.Any(static item => item.isMissing) || asset.audio.Select(static item => item.resourceId).Distinct(StringComparer.Ordinal).Count() != asset.audio.Length)
            throw new InvalidOperationException("Audio resource keys must be unique and available.");
        if (asset.performances.Any(static item => item.isMissing) || asset.performances.Select(static item => item.resourceId).Distinct(StringComparer.Ordinal).Count() != asset.performances.Length)
            throw new InvalidOperationException("Performance resource keys must be unique and available.");
        if (asset.particles.Any(static item => item.isMissing) || asset.particles.Select(static item => item.resourceId).Distinct(StringComparer.Ordinal).Count() != asset.particles.Length)
            throw new InvalidOperationException("Particle resource keys must be unique and available.");
        foreach (VisualEffectAsset preset in asset.particles) _ = new Inno.VisualNovel.Stage.Effects.ParticleSimulator(preset);
        _ = new Inno.VisualNovel.Stage.Effects.ScreenEffectController(asset.screenEffects);
        _ = new Inno.VisualNovel.Stage.Transitions.StageTransitionController(asset.transitions);
        if ((asset.screenEffects.Length > 0 || asset.transitions.Length > 0
            || asset.stages.Any(static stage => stage.layers.Any(static layer => layer.mask >= 0 || layer.surface.blend != Model.Stages.StageBlendMode.Normal))
            || asset.characters.Any(static character => character.poses.Any(static pose => pose.parts.Any(static part => part.mask >= 0 || part.surface.blend != Model.Stages.StageBlendMode.Normal))))
            && asset.effectsMaterial is not { isMissing: false })
            throw new InvalidOperationException("Screen effects, transitions, stage masks and blend modes require an imported VN image-composition material.");
        foreach (var chapter in asset.chapters)
            foreach (var instruction in chapter.program!.instructions)
            {
                if (instruction.screenEffect is { operation: ScreenEffectOperation.Set or ScreenEffectOperation.Fade } effect && !asset.screenEffects.Any(item => item.resourceId == effect.resourceId))
                    throw new InvalidOperationException($"Screen effect '{effect.resourceId}' is missing at {chapter.chapterId}/{instruction.nodeId}.");
                if (instruction.transition is { operation: TransitionOperation.Begin } transition && (!asset.transitions.Any(item => item.resourceId == transition.resourceId)
                    || transition.nextStage.Length > 0 && !asset.stages.Any(item => item.resourceId == transition.nextStage)))
                    throw new InvalidOperationException($"Transition or destination scenery is missing at {chapter.chapterId}/{instruction.nodeId}.");
            }
        if (asset.screens.Any(static item => item.screen is null || item.screen.isMissing || !SystemNodeRules.IsPageRole(item.role)) || asset.screens.Select(static item => item.role).Distinct(StringComparer.Ordinal).Count() != asset.screens.Length)
            throw new InvalidOperationException("Game screen roles must be unique and assigned.");
        foreach (string role in new[] { "title", "reading", "pause", "settings", "history", "credits" })
            if (!asset.screens.Any(item => item.role == role)) throw new InvalidOperationException("A game lacks its editable " + role + " screen.");
        _ = new StoryProgramLibrary(asset);
        foreach (var definition in asset.commandDefinitions) Model.Extensions.CommandContract.Validate(definition);
        if (asset.commandDefinitions.Select(static definition => definition.id).Distinct(StringComparer.Ordinal).Count() != asset.commandDefinitions.Length)
            throw new InvalidOperationException("Command catalog contains duplicate stable identities.");
        foreach (var command in asset.chapters.SelectMany(static chapter => chapter.program!.instructions).Where(static instruction => instruction.command is not null).Select(static instruction => instruction.command!))
        {
            var declared = asset.commandDefinitions.SingleOrDefault(definition => definition.id == command.definition.id);
            if (declared is null || Model.Extensions.CommandContract.Fingerprint(declared) != Model.Extensions.CommandContract.Fingerprint(command.definition))
                throw new InvalidOperationException("Game command schema is missing or differs from its story: " + command.definition.id);
        }
        CollectionProjectValidator.Validate(asset);
        Inno.VisualNovel.Authoring.UiEditing.UiSelectorValidator.Validate(asset);
        Inno.VisualNovel.Authoring.UiEditing.UiActionBindingValidator.Validate(asset);
        Inno.VisualNovel.Authoring.UiEditing.UiDataBindingValidator.Validate(asset);
        _ = new Inno.VisualNovel.Player.Interface.NovelPageStateStore(asset.screens.Concat(asset.themes.SelectMany(static theme => theme.screens)));
        _ = new Inno.VisualNovel.Player.Localization.NovelLocalizationService(asset.localization, asset.defaultLanguage);
        if (asset.localization is { } texts)
            foreach (var row in texts.entries)
                foreach (var translated in row.translations)
                    if (translated.voiceCueId.Length > 0 && !asset.audio.Any(cue => cue.resourceId == translated.voiceCueId && cue.channel == Model.Audio.NovelAudioChannel.Voice && !cue.loop))
                        throw new InvalidOperationException("Localized voice is unavailable at " + row.id + "/" + translated.language + ": " + translated.voiceCueId);
        _ = Inno.VisualNovel.Player.Audio.NovelAudioLibrary.Create(asset);
        PresentationValidator.Validate(asset);
        ThemeProjectValidator.Validate(asset);
    }
}

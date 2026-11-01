using System.Linq;

namespace Inno.VisualNovel.Model.Project;

/// <summary>Copies definition settings while retaining explicit imported asset identities.</summary>
public static class NovelProjectCopy
{
    /// <summary>Creates a detached project definition for candidate authoring and preview composition.</summary>
    /// <param name="source">The canonical or detached game definition.</param>
    /// <returns>Independent settings arrays and command schemas; referenced assets remain imported resources.</returns>
    public static NovelProjectAsset Copy(NovelProjectAsset source) => new()
    {
        gameId = source.gameId, buildFingerprint = source.buildFingerprint, title = source.title, localization = source.localization,
        collections = source.collections, commandDefinitions = source.commandDefinitions.Select(Extensions.CommandContract.Copy).ToArray(),
        uiActions = source.uiActions.Select(Interface.Actions.UiActionContract.Copy).ToArray(),
        uiDataSources = source.uiDataSources.Select(Interface.Data.UiDataSourceContract.Copy).ToArray(),
        additionalResources = source.additionalResources.ToArray(), defaultLanguage = source.defaultLanguage,
        chapters = source.chapters.ToArray(), stages = source.stages.ToArray(), characters = source.characters.ToArray(), audio = source.audio.ToArray(),
        performances = source.performances.ToArray(), particles = source.particles.ToArray(), screenEffects = source.screenEffects.ToArray(), transitions = source.transitions.ToArray(),
        effectsMaterial = source.effectsMaterial, initialStage = source.initialStage, startChapter = source.startChapter, startEntry = source.startEntry,
        screens = source.screens.Select(static binding => new NovelScreenBinding { role = binding.role, screen = binding.screen }).ToArray(),
        themes = source.themes.ToArray(), defaultTheme = source.defaultTheme,
        font = source.font, material = source.material, titleAudio = source.titleAudio, rollbackLimit = source.rollbackLimit,
        autoSaveOnChoices = source.autoSaveOnChoices, manualSaveSlots = source.manualSaveSlots, automaticSaveSlots = source.automaticSaveSlots, width = source.width, height = source.height
    };
}
